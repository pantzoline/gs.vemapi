'use client';

import type { TimelineEvent, TimelineEventType } from '@/types/order.types';
import {
  CheckCircle2, Clock, CreditCard, FileText, FileX, Video,
  Zap, AlertTriangle, Award, XCircle, RotateCcw, Send,
  MessageSquare, ArrowUpRight
} from 'lucide-react';
import { cn } from '@/lib/utils';

// ─── Mapa de ícones e cores por tipo de evento ────────────────────────────────
const EVENT_META: Record<TimelineEventType, {
  icon: React.ElementType;
  iconColor: string;
  ringColor: string;
  bgColor: string;
}> = {
  OrderCreated:              { icon: FileText,     iconColor: 'text-gray-400',   ringColor: 'ring-gray-600',   bgColor: 'bg-gray-800' },
  PaymentRequested:          { icon: CreditCard,   iconColor: 'text-amber-400',  ringColor: 'ring-amber-500',  bgColor: 'bg-amber-950' },
  PaymentConfirmed:          { icon: CheckCircle2, iconColor: 'text-blue-400',   ringColor: 'ring-blue-500',   bgColor: 'bg-blue-950' },
  PaymentFailed:             { icon: AlertTriangle,iconColor: 'text-red-400',    ringColor: 'ring-red-500',    bgColor: 'bg-red-950' },
  DocumentsRequested:        { icon: FileText,     iconColor: 'text-orange-400', ringColor: 'ring-orange-500', bgColor: 'bg-orange-950' },
  DocumentsValidated:        { icon: CheckCircle2, iconColor: 'text-cyan-400',   ringColor: 'ring-cyan-500',   bgColor: 'bg-cyan-950' },
  DocumentsRejected:         { icon: FileX,        iconColor: 'text-red-400',    ringColor: 'ring-red-500',    bgColor: 'bg-red-950' },
  VideoConferenceScheduled:  { icon: Video,        iconColor: 'text-violet-400', ringColor: 'ring-violet-500', bgColor: 'bg-violet-950' },
  VideoConferenceCompleted:  { icon: Video,        iconColor: 'text-violet-300', ringColor: 'ring-violet-400', bgColor: 'bg-violet-900' },
  SubmittedToAc:             { icon: Send,         iconColor: 'text-indigo-400', ringColor: 'ring-indigo-500', bgColor: 'bg-indigo-950' },
  ProtocolReceived:          { icon: Award,        iconColor: 'text-indigo-300', ringColor: 'ring-indigo-400', bgColor: 'bg-indigo-900' },
  AcStatusUpdated:           { icon: Zap,          iconColor: 'text-indigo-400', ringColor: 'ring-indigo-500', bgColor: 'bg-indigo-950' },
  AcError:                   { icon: AlertTriangle,iconColor: 'text-red-400',    ringColor: 'ring-red-500',    bgColor: 'bg-red-950' },
  CertificateIssued:         { icon: Award,        iconColor: 'text-emerald-400',ringColor: 'ring-emerald-500',bgColor: 'bg-emerald-950' },
  OrderCancelled:            { icon: XCircle,      iconColor: 'text-gray-400',   ringColor: 'ring-gray-600',   bgColor: 'bg-gray-800' },
  OrderRefunded:             { icon: RotateCcw,    iconColor: 'text-pink-400',   ringColor: 'ring-pink-500',   bgColor: 'bg-pink-950' },
  PaymentLinkResent:         { icon: Send,         iconColor: 'text-amber-400',  ringColor: 'ring-amber-500',  bgColor: 'bg-amber-950' },
  SystemNote:                { icon: MessageSquare,iconColor: 'text-gray-400',   ringColor: 'ring-gray-600',   bgColor: 'bg-gray-800' },
};

// ─── Nó individual da timeline ────────────────────────────────────────────────
function TimelineNode({ event, isLast }: { event: TimelineEvent; isLast: boolean }) {
  const meta = EVENT_META[event.type] ?? EVENT_META.SystemNote;
  const Icon = meta.icon;

  const ts = new Date(event.occurredAt);
  const timeStr = ts.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
  const dateStr = ts.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });

  let additionalJson: Record<string, unknown> | null = null;
  if (event.additionalData) {
    try { additionalJson = JSON.parse(event.additionalData); } catch { /* skip */ }
  }

  return (
    <div className="flex gap-4">
      {/* Coluna esquerda: ícone + linha conectora */}
      <div className="flex flex-col items-center">
        <div className={cn(
          'relative z-10 flex h-9 w-9 shrink-0 items-center justify-center rounded-full',
          'ring-2 shadow-lg transition-transform hover:scale-110',
          meta.bgColor, meta.ringColor,
          event.isError && 'animate-pulse ring-red-500 bg-red-950'
        )}>
          <Icon className={cn('h-4 w-4', meta.iconColor)} />
        </div>

        {/* Linha vertical conectora */}
        {!isLast && (
          <div className="mt-1 w-px flex-1 bg-gradient-to-b from-gray-600 to-transparent min-h-8" />
        )}
      </div>

      {/* Coluna direita: conteúdo */}
      <div className={cn(
        'flex-1 pb-6 pt-1',
        !isLast && 'border-b border-gray-800/50 mb-1'
      )}>
        {/* Header do evento */}
        <div className="flex items-start justify-between gap-2 mb-1">
          <h4 className={cn(
            'text-sm font-semibold leading-tight',
            event.isError ? 'text-red-400' : 'text-gray-100'
          )}>
            {event.title}
            {event.isError && (
              <span className="ml-2 text-xs font-normal px-1.5 py-0.5 rounded-full
                               bg-red-500/20 text-red-400 border border-red-500/30">
                ERRO
              </span>
            )}
          </h4>

          {/* Timestamp */}
          <div className="text-right shrink-0">
            <p className="text-gray-400 text-xs font-medium">{timeStr}</p>
            <p className="text-gray-600 text-xs">{dateStr}</p>
          </div>
        </div>

        {/* Descrição */}
        <p className="text-gray-400 text-sm leading-relaxed">{event.description}</p>

        {/* Dados adicionais (ex: resposta da AC, motivo de rejeição) */}
        {additionalJson && (
          <div className="mt-2 p-2.5 bg-gray-900 border border-gray-700/60 rounded-lg">
            {Object.entries(additionalJson).map(([key, value]) => (
              <div key={key} className="flex gap-2 text-xs">
                <span className="text-gray-500 font-medium min-w-24">{key}:</span>
                <span className="text-gray-300 font-mono break-all">{String(value)}</span>
              </div>
            ))}
          </div>
        )}

        {/* Agente responsável */}
        {event.triggeredByUserName && (
          <p className="mt-1.5 text-gray-600 text-xs">
            por <span className="text-gray-500">{event.triggeredByUserName}</span>
          </p>
        )}
      </div>
    </div>
  );
}

// ─── Skeleton da Timeline ────────────────────────────────────────────────────
export function TimelineSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      {[...Array(4)].map((_, i) => (
        <div key={i} className="flex gap-4">
          <div className="flex flex-col items-center">
            <div className="w-9 h-9 rounded-full bg-gray-700/70" />
            {i < 3 && <div className="w-px h-16 bg-gray-700/50 mt-1" />}
          </div>
          <div className="flex-1 pt-1">
            <div className="flex justify-between mb-2">
              <div className="h-3.5 w-40 bg-gray-700 rounded" />
              <div className="h-3 w-16 bg-gray-700/60 rounded" />
            </div>
            <div className="h-3 w-3/4 bg-gray-700/50 rounded" />
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── Estado Vazio ─────────────────────────────────────────────────────────────
function EmptyTimeline() {
  return (
    <div className="flex flex-col items-center gap-2 py-10">
      <Clock className="w-8 h-8 text-gray-600" />
      <p className="text-gray-500 text-sm">Nenhum evento registrado ainda.</p>
    </div>
  );
}

// ─── Componente Principal ─────────────────────────────────────────────────────
type OrderTimelineProps = {
  events: TimelineEvent[];
  isLoading?: boolean;
};

export function OrderTimeline({ events, isLoading }: OrderTimelineProps) {
  if (isLoading) return <TimelineSkeleton />;
  if (!events.length) return <EmptyTimeline />;

  // Ordena do mais recente para o mais antigo (ordem reversa = eventos novos no topo)
  const sorted = [...events].sort(
    (a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime()
  );

  return (
    <div className="relative">
      {sorted.map((event, idx) => (
        <TimelineNode
          key={event.id}
          event={event}
          isLast={idx === sorted.length - 1}
        />
      ))}
    </div>
  );
}
