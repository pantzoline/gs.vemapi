'use client';

import { useState } from 'react';
import { useOrderDetailLive, useResendPaymentLink, useTransitionStatus } from '@/hooks/useOrders';
import { OrderTimeline, TimelineSkeleton } from '@/components/orders/OrderTimeline';
import { STATUS_META, formatCents, formatDoc } from '@/types/order.types';
import {
  ArrowLeft, Send, FileDown, MessageSquare, Video,
  RefreshCw, Copy, ExternalLink, Clock, Shield, Award
} from 'lucide-react';
import { cn } from '@/lib/utils';
import Link from 'next/link';

type OrderDetailPageProps = { orderId: string };

// ─── Info Row simples ────────────────────────────────────────────────────────
function InfoRow({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex justify-between items-center py-2.5 border-b border-gray-700/30 last:border-0">
      <span className="text-gray-500 text-sm">{label}</span>
      <span className={cn('text-gray-200 text-sm font-medium', mono && 'font-mono text-xs')}>{value}</span>
    </div>
  );
}

// ─── Card de Protocolo AC ────────────────────────────────────────────────────
function ProtocolCard({ ticket, acProvider, videoUrl }: {
  ticket?: string;
  acProvider: string;
  videoUrl?: string;
}) {
  const [copied, setCopied] = useState(false);

  const copy = () => {
    if (!ticket) return;
    navigator.clipboard.writeText(ticket);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="p-4 bg-indigo-950/50 border border-indigo-700/40 rounded-xl">
      <div className="flex items-center gap-2 mb-3">
        <Award className="w-4 h-4 text-indigo-400" />
        <span className="text-indigo-300 text-sm font-semibold">Dados da AC — {acProvider}</span>
      </div>

      {ticket ? (
        <div className="space-y-2">
          <div className="flex items-center justify-between p-2 bg-indigo-900/40 rounded-lg">
            <div>
              <p className="text-gray-500 text-xs">Protocolo / Ticket</p>
              <p className="text-white font-mono text-sm font-semibold">{ticket}</p>
            </div>
            <button onClick={copy}
              className="flex items-center gap-1.5 px-2.5 py-1.5 bg-indigo-700/40
                         hover:bg-indigo-700/60 text-indigo-300 text-xs rounded-lg transition-all">
              <Copy className="w-3 h-3" />
              {copied ? 'Copiado!' : 'Copiar'}
            </button>
          </div>

          {videoUrl && (
            <a href={videoUrl} target="_blank" rel="noopener noreferrer"
              className="flex items-center gap-2 p-2.5 bg-violet-900/40
                        border border-violet-700/40 rounded-lg text-violet-300
                        hover:bg-violet-800/40 transition-all group text-sm">
              <Video className="w-4 h-4" />
              <span className="font-medium">Acessar Videoconferência</span>
              <ExternalLink className="w-3 h-3 ml-auto opacity-60 group-hover:opacity-100" />
            </a>
          )}
        </div>
      ) : (
        <div className="flex items-center gap-2 text-gray-500 text-sm">
          <Clock className="w-4 h-4 animate-pulse" />
          Protocolo ainda não gerado pela AC.
        </div>
      )}
    </div>
  );
}

// ─── Página de Detalhes ───────────────────────────────────────────────────────
export function OrderDetailPage({ orderId }: OrderDetailPageProps) {
  const { data: order, isLoading, refetch, isFetching } = useOrderDetailLive(orderId);
  const resendLink = useResendPaymentLink(orderId);

  if (isLoading) {
    return (
      <div className="min-h-screen bg-gray-950 p-6">
        <div className="max-w-5xl mx-auto animate-pulse space-y-4">
          <div className="h-6 w-32 bg-gray-700 rounded" />
          <div className="h-10 w-64 bg-gray-700 rounded" />
          <div className="grid grid-cols-3 gap-4">
            {[...Array(3)].map((_, i) => (
              <div key={i} className="h-32 bg-gray-800 rounded-2xl" />
            ))}
          </div>
          <div className="h-96 bg-gray-800 rounded-2xl" />
        </div>
      </div>
    );
  }

  if (!order) return null;

  const statusMeta = STATUS_META[order.status];
  const isTerminal = ['Issued', 'Cancelled', 'Expired', 'Refunded'].includes(order.status);

  return (
    <div className="min-h-screen bg-gray-950 text-white">
      <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 space-y-6">

        {/* ─── Breadcrumb e Refresh ──────────────────────────────────────── */}
        <div className="flex items-center justify-between">
          <Link href="/dashboard/orders"
            className="flex items-center gap-2 text-gray-500 hover:text-gray-200
                       text-sm transition-colors">
            <ArrowLeft className="w-4 h-4" />
            Voltar à lista
          </Link>

          <button onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-gray-700
                       text-gray-400 hover:text-white text-sm transition-all
                       disabled:opacity-40">
            <RefreshCw className={cn('w-3.5 h-3.5', isFetching && 'animate-spin')} />
            {isFetching ? 'Consultando AC...' : 'Atualizar'}
          </button>
        </div>

        {/* ─── Header do Pedido ──────────────────────────────────────────── */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-3 mb-1">
              <h1 className="text-2xl font-bold text-white">{order.orderNumber}</h1>
              <span className={cn(
                'flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-medium',
                statusMeta.color, statusMeta.bg
              )}>
                <span className="w-2 h-2 rounded-full" style={{ backgroundColor: statusMeta.dot }} />
                {statusMeta.label}
              </span>
            </div>
            <p className="text-gray-400 text-sm">{order.productName} · {order.acProvider}</p>
          </div>

          {/* ─── Ações Rápidas ────────────────────────────────────────────── */}
          <div className="flex items-center gap-2">
            {order.status === 'PendingPayment' && (
              <button
                onClick={() => resendLink.mutate()}
                disabled={resendLink.isPending}
                className="flex items-center gap-2 px-3 py-2 bg-amber-600/20
                           border border-amber-500/40 text-amber-400 text-sm
                           rounded-xl hover:bg-amber-600/30 transition-all">
                <Send className="w-4 h-4" />
                {resendLink.isPending ? 'Enviando...' : 'Reenviar Link'}
              </button>
            )}

            <button className="flex items-center gap-2 px-3 py-2 bg-gray-800
                               border border-gray-700 text-gray-400 text-sm
                               rounded-xl hover:text-white transition-all">
              <FileDown className="w-4 h-4" />
              Termo
            </button>

            <button className="flex items-center gap-2 px-3 py-2 bg-gray-800
                               border border-gray-700 text-gray-400 text-sm
                               rounded-xl hover:text-white transition-all">
              <MessageSquare className="w-4 h-4" />
              Suporte
            </button>
          </div>
        </div>

        {/* ─── Grid de informações ──────────────────────────────────────────── */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">

          {/* Cliente */}
          <div className="p-4 bg-gray-800/50 border border-gray-700/40 rounded-2xl">
            <h3 className="text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
              Cliente
            </h3>
            <InfoRow label="Nome"      value={order.customerName} />
            <InfoRow label="Documento" value={formatDoc(order.customerDocument)} mono />
            <InfoRow label="E-mail"    value={order.customerEmail} />
          </div>

          {/* Financeiro */}
          <div className="p-4 bg-gray-800/50 border border-gray-700/40 rounded-2xl">
            <h3 className="text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
              Financeiro
            </h3>
            <InfoRow label="Valor Total"   value={formatCents(order.totalAmountInCents)} />
            <InfoRow label="Receita Líq."  value={formatCents(order.netAmountInCents)} />
            <InfoRow label="PA"            value={order.paName} />
            <InfoRow label="AR"            value={order.arName} />
          </div>

          {/* Datas */}
          <div className="p-4 bg-gray-800/50 border border-gray-700/40 rounded-2xl">
            <h3 className="text-gray-400 text-xs font-semibold uppercase tracking-wider mb-3">
              Datas
            </h3>
            <InfoRow label="Abertura"
              value={new Date(order.createdAt).toLocaleString('pt-BR')} />
            {order.issuedAt && (
              <InfoRow label="Emissão"
                value={new Date(order.issuedAt).toLocaleString('pt-BR')} />
            )}
            <InfoRow label="Tipo"
              value={`${order.certificationType} · ${order.acProvider}`} />
          </div>
        </div>

        {/* Protocolo AC */}
        <ProtocolCard
          ticket={order.protocolTicket}
          acProvider={order.acProvider}
          videoUrl={order.videoConferenceUrl}
        />

        {/* ─── Timeline ─────────────────────────────────────────────────────── */}
        <div className="p-5 bg-gray-800/50 border border-gray-700/40 rounded-2xl">
          <div className="flex items-center gap-2 mb-5">
            <Shield className="w-4 h-4 text-gray-400" />
            <h3 className="text-gray-200 font-semibold">Histórico Imutável do Pedido</h3>
            <span className="text-gray-600 text-xs ml-auto">
              {order.timeline.length} eventos
            </span>
          </div>
          <OrderTimeline events={order.timeline} />
        </div>

      </div>
    </div>
  );
}
