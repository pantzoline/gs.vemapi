'use client';

import {
  HubConnection, HubConnectionBuilder, HubConnectionState,
  LogLevel, HttpTransportType
} from '@microsoft/signalr';
import { useEffect, useRef, useState, useCallback } from 'react';
import { getAccessToken } from '@/lib/api-client';
import type { AppNotification, SignalRConnectionStatus } from '@/types/notification.types';

const HUB_URL = `${process.env.NEXT_PUBLIC_API_URL}/hubs/notifications`;
const MAX_HISTORY = 20; // Máximo de notificações no histórico de sessão

type UseSignalROptions = {
  onNotification: (notification: AppNotification) => void;
};

type UseSignalRReturn = {
  status: SignalRConnectionStatus;
  isConnected: boolean;
  markAsRead: (id: string) => Promise<void>;
};

export function useSignalR({ onNotification }: UseSignalROptions): UseSignalRReturn {
  const connectionRef = useRef<HubConnection | null>(null);
  const [status, setStatus] = useState<SignalRConnectionStatus>('disconnected');

  // ─── Cria e gerencia a conexão ────────────────────────────────────────────
  useEffect(() => {
    // Não conecta se não há token (usuário não está logado)
    const token = getAccessToken();
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, {
        // JWT enviado via query string (WebSocket não suporta headers custom)
        accessTokenFactory: () => getAccessToken() ?? '',

        // Fallback automático: WS → SSE → Long Polling
        // Garante funcionamento em redes corporativas que bloqueiam WS
        transport:
          HttpTransportType.WebSockets |
          HttpTransportType.ServerSentEvents |
          HttpTransportType.LongPolling,
      })
      .withAutomaticReconnect({
        // Estratégia de Exponential Backoff customizada
        // Tenta: imediato → 2s → 5s → 10s → 15s → 30s → 60s...
        nextRetryDelayInMilliseconds: (retryContext) => {
          const delays = [0, 2000, 5000, 10000, 15000, 30000, 60000];
          return delays[Math.min(retryContext.previousRetryCount, delays.length - 1)];
        }
      })
      .configureLogging(
        process.env.NODE_ENV === 'development' ? LogLevel.Information : LogLevel.Warning
      )
      .build();

    connectionRef.current = connection;

    // ─── Listeners de ciclo de vida da conexão ────────────────────────────
    connection.onreconnecting(() => {
      setStatus('reconnecting');
      console.info('[SignalR] Reconectando...');
    });

    connection.onreconnected(() => {
      setStatus('connected');
      console.info('[SignalR] Reconectado!');
    });

    connection.onclose((err) => {
      setStatus('disconnected');
      if (err) console.error('[SignalR] Conexão encerrada com erro:', err);
    });

    // ─── Listener principal: recebe notificações do backend ──────────────
    // O nome "ReceiveNotification" DEVE bater com o CLIENT_METHOD do C#
    connection.on('ReceiveNotification', (rawPayload: Omit<AppNotification, 'isRead'>) => {
      const notification: AppNotification = { ...rawPayload, isRead: false };
      onNotification(notification);
    });

    // ─── Conecta ──────────────────────────────────────────────────────────
    const start = async () => {
      try {
        setStatus('connecting');
        await connection.start();
        setStatus('connected');
        console.info('[SignalR] Conectado ao NotificationHub');
      } catch (err) {
        setStatus('disconnected');
        console.error('[SignalR] Falha ao conectar:', err);
      }
    };

    start();

    // ─── Limpeza ao desmontar ─────────────────────────────────────────────
    return () => {
      connection.off('ReceiveNotification');
      connection.stop().catch(console.error);
    };
    // onNotification é estável via useCallback no Provider — sem loop infinito
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // ─── Marcar como lida (invoca método no Hub) ──────────────────────────────
  const markAsRead = useCallback(async (notificationId: string) => {
    const conn = connectionRef.current;
    if (conn?.state === HubConnectionState.Connected) {
      await conn.invoke('MarkAsRead', notificationId);
    }
  }, []);

  return {
    status,
    isConnected: status === 'connected',
    markAsRead,
  };
}
