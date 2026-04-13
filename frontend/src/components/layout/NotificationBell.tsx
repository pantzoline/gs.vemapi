'use client';

import { useState, useRef, useEffect } from 'react';
import { useNotifications } from '@/providers/SignalRProvider';
import { NOTIFICATION_META, type AppNotification } from '@/types/notification.types';
import { Bell, BellRing, CheckCheck, Trash2, Wifi, WifiOff, Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';
import { useRouter } from 'next/navigation';

// ─── Status da conexão ────────────────────────────────────────────────────────
function ConnectionDot({ status }: { status: string }) {
  const configs = {
    connected:    { color: 'bg-emerald-500', pulse: false, label: 'Online' },
    reconnecting: { color: 'bg-amber-500',   pulse: true,  label: 'Reconectando' },
    connecting:   { color: 'bg-blue-500',    pulse: true,  label: 'Conectando' },
    disconnected: { color: 'bg-red-500',     pulse: false, label: 'Offline' },
  };
  const cfg = configs[status as keyof typeof configs] ?? configs.disconnected;

  return (
    <div className="flex items-center gap-1.5" title={cfg.label}>
      <div className={cn('w-1.5 h-1.5 rounded-full', cfg.color, cfg.pulse && 'animate-pulse')} />
      <span className="text-xs text-gray-500 hidden sm:block">{cfg.label}</span>
    </div>
  );
}

// ─── Item individual de notificação no dropdown ───────────────────────────────
function NotificationItem({
  notification,
  onRead,
  onAction
}: {
  notification: AppNotification;
  onRead: (id: string) => void;
  onAction: (url: string) => void;
}) {
  const meta = NOTIFICATION_META[notification.type];
  const ts = new Date(notification.issuedAt);
  const timeAgo = formatTimeAgo(ts);

  return (
    <div
      onClick={() => {
        onRead(notification.id);
        if (notification.actionUrl) onAction(notification.actionUrl);
      }}
      className={cn(
        'flex gap-3 p-3 rounded-xl cursor-pointer transition-all duration-200',
        'hover:bg-gray-700/40 group',
        !notification.isRead && 'bg-gray-700/20'
      )}
    >
      {/* Ícone do tipo */}
      <div className={cn(
        'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-base',
        meta.bg, meta.border, 'border'
      )}>
        {meta.icon}
      </div>

      {/* Conteúdo */}
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-1">
          <p className={cn(
            'text-sm font-medium leading-tight truncate',
            notification.isRead ? 'text-gray-300' : 'text-white'
          )}>
            {notification.title}
          </p>
          <span className="text-gray-600 text-xs shrink-0 mt-0.5">{timeAgo}</span>
        </div>
        <p className="text-gray-400 text-xs mt-0.5 leading-relaxed line-clamp-2">
          {notification.message}
        </p>
        {notification.actionLabel && (
          <span className={cn('text-xs font-medium mt-1 block', meta.color)}>
            {notification.actionLabel} →
          </span>
        )}
      </div>

      {/* Indicador de não lida */}
      {!notification.isRead && (
        <div className="w-1.5 h-1.5 rounded-full bg-blue-500 shrink-0 mt-2" />
      )}
    </div>
  );
}

// ─── Componente Principal: NotificationBell ───────────────────────────────────
export function NotificationBell() {
  const router = useRouter();
  const { notifications, unreadCount, connectionStatus, markAsRead, markAllAsRead, clearHistory } =
    useNotifications();

  const [isOpen, setIsOpen] = useState(false);
  const panelRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  // Fecha ao clicar fora
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (
        panelRef.current && !panelRef.current.contains(e.target as Node) &&
        buttonRef.current && !buttonRef.current.contains(e.target as Node)
      ) {
        setIsOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  // Marca todas como lidas ao abrir o painel
  const handleOpen = () => {
    setIsOpen(v => {
      if (!v && unreadCount > 0) {
        // Pequeno delay visual antes de marcar como lido
        setTimeout(markAllAsRead, 1500);
      }
      return !v;
    });
  };

  const handleAction = (url: string) => {
    setIsOpen(false);
    router.push(url);
  };

  const isEmpty = notifications.length === 0;
  const isAnimating = unreadCount > 0;

  return (
    <div className="relative">
      {/* ─── Botão do Sininho ──────────────────────────────────────────── */}
      <button
        ref={buttonRef}
        onClick={handleOpen}
        className={cn(
          'relative flex items-center justify-center',
          'w-9 h-9 rounded-xl border transition-all duration-200',
          isOpen
            ? 'bg-gray-700 border-gray-600 text-white'
            : 'bg-gray-800/60 border-gray-700/60 text-gray-400 hover:text-white hover:border-gray-600'
        )}
        aria-label={`Notificações${unreadCount > 0 ? ` (${unreadCount} não lidas)` : ''}`}
      >
        {isAnimating
          ? <BellRing className="w-4 h-4 animate-[wiggle_0.5s_ease-in-out_infinite]" />
          : <Bell className="w-4 h-4" />
        }

        {/* Badge de contagem */}
        {unreadCount > 0 && (
          <span className={cn(
            'absolute -top-1 -right-1 flex items-center justify-center',
            'w-4 h-4 rounded-full bg-red-500 text-white text-[10px] font-bold',
            'animate-in zoom-in duration-200'
          )}>
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      {/* ─── Painel de Notificações ───────────────────────────────────── */}
      {isOpen && (
        <div
          ref={panelRef}
          className={cn(
            'absolute right-0 mt-2 w-80 sm:w-96',
            'bg-gray-900 border border-gray-700/60 rounded-2xl shadow-2xl shadow-black/50',
            'animate-in fade-in slide-in-from-top-2 duration-200',
            'z-50 overflow-hidden'
          )}
        >
          {/* Header do painel */}
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-700/40">
            <div className="flex items-center gap-2">
              <Bell className="w-4 h-4 text-gray-400" />
              <span className="text-gray-200 font-semibold text-sm">Notificações</span>
              {unreadCount > 0 && (
                <span className="px-1.5 py-0.5 bg-blue-600/30 text-blue-400
                                 text-xs rounded-full font-medium">
                  {unreadCount} nova{unreadCount !== 1 ? 's' : ''}
                </span>
              )}
            </div>

            <div className="flex items-center gap-2">
              <ConnectionDot status={connectionStatus} />

              {!isEmpty && (
                <button
                  onClick={clearHistory}
                  className="p-1 rounded-lg text-gray-600 hover:text-red-400
                             hover:bg-red-900/20 transition-all"
                  title="Limpar histórico"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                </button>
              )}
            </div>
          </div>

          {/* Lista de Notificações */}
          <div className="max-h-96 overflow-y-auto overscroll-contain">
            {connectionStatus === 'connecting' || connectionStatus === 'reconnecting' ? (
              <div className="flex items-center justify-center gap-2 py-8 text-gray-500">
                <Loader2 className="w-4 h-4 animate-spin" />
                <span className="text-sm">
                  {connectionStatus === 'connecting' ? 'Conectando...' : 'Reconectando...'}
                </span>
              </div>
            ) : isEmpty ? (
              <div className="flex flex-col items-center gap-2 py-10">
                <Bell className="w-8 h-8 text-gray-700" />
                <p className="text-gray-500 text-sm font-medium">Nenhuma notificação</p>
                <p className="text-gray-600 text-xs text-center px-6">
                  Alertas de pagamentos e emissões aparecerão aqui em tempo real.
                </p>
              </div>
            ) : (
              <div className="p-2 space-y-0.5">
                {notifications.map(n => (
                  <NotificationItem
                    key={n.id}
                    notification={n}
                    onRead={markAsRead}
                    onAction={handleAction}
                  />
                ))}
              </div>
            )}
          </div>

          {/* Footer com status de conexão */}
          {connectionStatus === 'disconnected' && (
            <div className="px-4 py-2.5 bg-red-900/20 border-t border-red-700/30
                            flex items-center gap-2">
              <WifiOff className="w-3.5 h-3.5 text-red-400" />
              <span className="text-red-400 text-xs">
                Sem conexão em tempo real. Recarregue a página.
              </span>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Utilitário: tempo relativo ───────────────────────────────────────────────
function formatTimeAgo(date: Date): string {
  const seconds = Math.floor((Date.now() - date.getTime()) / 1000);
  if (seconds < 5)  return 'agora';
  if (seconds < 60) return `${seconds}s`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}min`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24)   return `${hours}h`;
  return date.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
}
