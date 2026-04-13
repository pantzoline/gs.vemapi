// Espelha os tipos do backend 1:1
export type CertificationType = 'A1' | 'A3' | 'Cloud';
export type AcProvider = 'SYNGULAR' | 'VALID' | 'SAFEWEB' | string;

export type OrderStatus =
  | 'Draft'
  | 'PendingPayment'
  | 'Paid'
  | 'DocumentPendingValidation'
  | 'DocumentValidated'
  | 'DocumentRejected'
  | 'AwaitingVideoConference'
  | 'VideoConferenceCompleted'
  | 'IssuingAtAc'
  | 'Issued'
  | 'AcError'
  | 'Cancelled'
  | 'Expired'
  | 'Refunded';

export type TimelineEventType =
  | 'OrderCreated' | 'PaymentRequested' | 'PaymentConfirmed' | 'PaymentFailed'
  | 'DocumentsRequested' | 'DocumentsValidated' | 'DocumentsRejected'
  | 'VideoConferenceScheduled' | 'VideoConferenceCompleted'
  | 'SubmittedToAc' | 'ProtocolReceived' | 'AcStatusUpdated' | 'AcError' | 'CertificateIssued'
  | 'OrderCancelled' | 'OrderRefunded' | 'PaymentLinkResent' | 'SystemNote';

export type TimelineEvent = {
  id: string;
  orderId: string;
  type: TimelineEventType;
  title: string;
  description: string;
  additionalData?: string; // JSON string
  isError: boolean;
  triggeredByUserId: string;
  triggeredByUserName?: string;
  occurredAt: string; // ISO 8601
};

export type OrderDetail = {
  id: string;
  orderNumber: string;
  paId: string;
  paName: string;
  arName: string;
  agentUserId: string;
  productCode: string;
  productName: string;
  certificationType: CertificationType;
  acProvider: AcProvider;
  customerName: string;
  customerDocument: string;
  customerEmail: string;
  totalAmountInCents: number;
  netAmountInCents: number;
  protocolTicket?: string;
  videoConferenceUrl?: string;
  issuedAt?: string;
  status: OrderStatus;
  timeline: TimelineEvent[];
  createdAt: string;
  updatedAt: string;
};

export type OrderSummary = {
  id: string;
  orderNumber: string;
  customerName: string;
  customerDocument: string;
  productName: string;
  acProvider: AcProvider;
  certificationType: string;
  status: OrderStatus;
  statusLabel: string;
  totalAmountInCents: number;
  protocolTicket?: string;
  paName: string;
  createdAt: string;
};

export type OrderListResult = {
  items: OrderSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPrevPage: boolean;
};

export type OrderListQuery = {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortDesc?: boolean;
  statuses?: OrderStatus[];
  acProviders?: string[];
  certTypes?: CertificationType[];
  from?: string;
  to?: string;
};

// ─── Metadados de UI por status ──────────────────────────────────────────────
export const STATUS_META: Record<OrderStatus, {
  label: string;
  color: string;
  bg: string;
  dot: string;
}> = {
  Draft:                    { label: 'Rascunho',         color: 'text-gray-400',   bg: 'bg-gray-400/10',   dot: '#9ca3af' },
  PendingPayment:           { label: 'Aguard. Pagamento', color: 'text-amber-400',  bg: 'bg-amber-400/10',  dot: '#f59e0b' },
  Paid:                     { label: 'Pago',              color: 'text-blue-400',   bg: 'bg-blue-400/10',   dot: '#3b82f6' },
  DocumentPendingValidation:{ label: 'Doc. Pendentes',    color: 'text-orange-400', bg: 'bg-orange-400/10', dot: '#fb923c' },
  DocumentValidated:        { label: 'Doc. Validados',    color: 'text-cyan-400',   bg: 'bg-cyan-400/10',   dot: '#22d3ee' },
  DocumentRejected:         { label: 'Doc. Rejeitados',   color: 'text-red-400',    bg: 'bg-red-400/10',    dot: '#f87171' },
  AwaitingVideoConference:  { label: 'Aguard. Vídeo',     color: 'text-violet-400', bg: 'bg-violet-400/10', dot: '#a78bfa' },
  VideoConferenceCompleted: { label: 'Vídeo OK',           color: 'text-violet-300', bg: 'bg-violet-300/10', dot: '#c4b5fd' },
  IssuingAtAc:              { label: 'Emitindo na AC',    color: 'text-indigo-400', bg: 'bg-indigo-400/10', dot: '#818cf8' },
  Issued:                   { label: 'Emitido ✓',         color: 'text-emerald-400',bg: 'bg-emerald-400/10',dot: '#34d399' },
  AcError:                  { label: 'Erro AC',            color: 'text-red-500',    bg: 'bg-red-500/10',    dot: '#ef4444' },
  Cancelled:                { label: 'Cancelado',          color: 'text-gray-500',   bg: 'bg-gray-500/10',   dot: '#6b7280' },
  Expired:                  { label: 'Expirado',           color: 'text-gray-500',   bg: 'bg-gray-500/10',   dot: '#6b7280' },
  Refunded:                 { label: 'Estornado',          color: 'text-pink-400',   bg: 'bg-pink-400/10',   dot: '#f472b6' },
};

export const formatCents = (cents: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100);

export const formatDoc = (doc: string) => {
  const d = doc.replace(/\D/g, '');
  if (d.length === 11) return d.replace(/(\d{3})(\d{3})(\d{3})(\d{2})/, '$1.$2.$3-$4');
  if (d.length === 14) return d.replace(/(\d{2})(\d{3})(\d{3})(\d{4})(\d{2})/, '$1.$2.$3/$4-$5');
  return doc;
};
