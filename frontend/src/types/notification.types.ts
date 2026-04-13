// Espelha o backend 1:1
export type NotificationType = 'Success' | 'Info' | 'Warning' | 'CriticalError' | 'Financial';

export type AppNotification = {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  actionUrl?: string;
  actionLabel?: string;
  relatedEntityId?: string;
  issuedAt: string; // ISO 8601
  isRead: boolean;  // Gerenciado no frontend (sessão atual)
};

// Metadados visuais por tipo
export const NOTIFICATION_META: Record<NotificationType, {
  icon: string;
  toastVariant: 'default' | 'destructive';
  color: string;
  bg: string;
  border: string;
  sound?: boolean;
}> = {
  Success:       { icon: '✅', toastVariant: 'default',     color: 'text-emerald-400', bg: 'bg-emerald-500/10', border: 'border-emerald-500/30', sound: true },
  Info:          { icon: 'ℹ️',  toastVariant: 'default',     color: 'text-blue-400',    bg: 'bg-blue-500/10',    border: 'border-blue-500/30'    },
  Warning:       { icon: '⚠️',  toastVariant: 'default',     color: 'text-amber-400',   bg: 'bg-amber-500/10',   border: 'border-amber-500/30'   },
  CriticalError: { icon: '🔴',  toastVariant: 'destructive', color: 'text-red-400',     bg: 'bg-red-500/10',     border: 'border-red-500/30',    sound: true },
  Financial:     { icon: '💰',  toastVariant: 'default',     color: 'text-violet-400',  bg: 'bg-violet-500/10',  border: 'border-violet-500/30', sound: true },
};

export type SignalRConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';
