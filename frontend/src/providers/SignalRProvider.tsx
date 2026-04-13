'use client';

import React, {
  createContext, useContext, useState, useCallback,
  useRef, useEffect, type ReactNode
} from 'react';
import { toast } from 'sonner';
import { useSignalR } from '@/hooks/useSignalR';
import { useAuth } from '@/contexts/AuthContext';
import type {
  AppNotification, SignalRConnectionStatus, NotificationType
} from '@/types/notification.types';
import { NOTIFICATION_META } from '@/types/notification.types';
import { useRouter } from 'next/navigation';

// ─── Context ──────────────────────────────────────────────────────────────────
type NotificationContextValue = {
  notifications: AppNotification[];
  unreadCount: number;
  connectionStatus: SignalRConnectionStatus;
  markAsRead: (id: string) => void;
  markAllAsRead: () => void;
  clearHistory: () => void;
};

const NotificationContext = createContext<NotificationContextValue | null>(null);

const MAX_HISTORY = 20;

// ─── Toast Renderer ───────────────────────────────────────────────────────────
function renderToast(
  notification: AppNotification,
  onAction?: (url: string) => void
) {
  const meta = NOTIFICATION_META[notification.type];

  // Toca som para notificações financeiras e erros críticos
  if (meta.sound && typeof window !== 'undefined') {
    try {
      const ctx = new AudioContext();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.frequency.value = notification.type === 'CriticalError' ? 300 : 880;
      gain.gain.setValueAtTime(0.1, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.3);
      osc.start(ctx.currentTime);
      osc.stop(ctx.currentTime + 0.3);
    } catch { /* AudioContext pode não estar disponível */ }
  }

  const toastFn = notification.type === 'CriticalError' ? toast.error : toast;

  toastFn(
    // Título customizado com ícone
    <div className="flex flex-col gap-1">
      <span className="font-semibold text-sm">{notification.title}</span>
      <span className="text-gray-400 text-xs leading-relaxed">{notification.message}</span>
      {notification.actionUrl && notification.actionLabel && (
        <button
          onClick={() => onAction?.(notification.actionUrl!)}
          className="mt-1 text-xs font-medium text-blue-400 hover:text-blue-300
                     text-left transition-colors w-fit"
        >
          {notification.actionLabel} →
        </button>
      )}
    </div>,
    {
      id:       notification.id,
      duration: notification.type === 'CriticalError' ? 10_000 : 5_000,
      position: 'bottom-right',
      style: {
        background: '#111827',
        border: `1px solid ${
          notification.type === 'CriticalError' ? '#ef444440' :
          notification.type === 'Financial'     ? '#8b5cf640' :
          notification.type === 'Success'       ? '#10b98140' :
          notification.type === 'Warning'       ? '#f59e0b40' :
          '#3b82f640'
        }`,
        color: '#f9fafb',
      },
    }
  );
}

// ─── Provider ─────────────────────────────────────────────────────────────────
export function SignalRProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth();
  const router = useRouter();
  const [notifications, setNotifications] = useState<AppNotification[]>([]);

  const handleAction = useCallback((url: string) => {
    router.push(url);
  }, [router]);

  // ─── Callback estável para o hook useSignalR ────────────────────────────
  // useCallback com deps vazias — cria a função uma vez. O router é estável.
  const handleNotification = useCallback((notification: AppNotification) => {
    // Adiciona ao histórico (máx MAX_HISTORY, mais recente no topo)
    setNotifications(prev => {
      const updated = [notification, ...prev];
      return updated.slice(0, MAX_HISTORY);
    });

    // Dispara o toast visual
    renderToast(notification, handleAction);
  }, [handleAction]);

  // ─── Conecta ao SignalR apenas se autenticado ─────────────────────────────
  const { status, markAsRead: hubMarkAsRead } = useSignalR(
    isAuthenticated
      ? { onNotification: handleNotification }
      : { onNotification: () => {} }
  );

  const markAsRead = useCallback((id: string) => {
    setNotifications(prev =>
      prev.map(n => n.id === id ? { ...n, isRead: true } : n)
    );
    hubMarkAsRead(id).catch(console.error);
  }, [hubMarkAsRead]);

  const markAllAsRead = useCallback(() => {
    setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
  }, []);

  const clearHistory = useCallback(() => setNotifications([]), []);

  const unreadCount = notifications.filter(n => !n.isRead).length;

  return (
    <NotificationContext.Provider value={{
      notifications,
      unreadCount,
      connectionStatus: status,
      markAsRead,
      markAllAsRead,
      clearHistory,
    }}>
      {children}
    </NotificationContext.Provider>
  );
}

// ─── Hook de consumo ─────────────────────────────────────────────────────────
export function useNotifications(): NotificationContextValue {
  const ctx = useContext(NotificationContext);
  if (!ctx) throw new Error('useNotifications deve estar dentro de <SignalRProvider>');
  return ctx;
}
