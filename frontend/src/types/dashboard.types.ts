// Espelha os DTOs do Backend 1:1 — nunca divergem
export type TrendDirection = 'Up' | 'Down' | 'Neutral';

export type KpiCard = {
  label: string;
  value: number;
  formattedValue: string;
  changePercent: number | null;
  trend: TrendDirection;
  unit?: string;
};

export type DateRange = { from: string; to: string };

export type DashboardSummary = {
  totalSales: KpiCard;
  netRevenue: KpiCard;
  averageTicket: KpiCard;
  protocolConversion: KpiCard;
  tenantName: string;
  tenantLevel: string;
  period: DateRange;
};

export type SalesTimeSeries = {
  date: string;         // "2025-01-15"
  totalRevenue: number;
  netRevenue: number;
  orderCount: number;
};

export type OrderStatusDistribution = {
  status: string;       // Traduzido: "Aguardando Pagamento"
  statusKey: string;    // "PENDING_PAYMENT"
  count: number;
  percentage: number;
  color: string;        // HEX para o Recharts
};

export type PartnerRanking = {
  rank: number;
  paId: string;
  paName: string;
  arName: string;
  totalOrders: number;
  totalRevenue: number;
  changePercent: number;
};

export type RevenueByProduct = {
  productName: string;
  productCode: string;
  revenue: number;
  quantity: number;
};

export type DashboardCharts = {
  salesTimeSeries: SalesTimeSeries[];
  statusDistribution: OrderStatusDistribution[];
  partnerRanking: PartnerRanking[] | null; // null = ocultar widget
  revenueByProduct: RevenueByProduct[];
};

// Preset de períodos para o filtro global
export type PeriodPreset = 'today' | '7d' | '30d' | '90d' | 'custom';

export type DashboardFilter = {
  preset: PeriodPreset;
  from: Date;
  to: Date;
};

export const PERIOD_PRESETS: Record<Exclude<PeriodPreset, 'custom'>, { label: string; days: number }> = {
  today: { label: 'Hoje', days: 0 },
  '7d':  { label: '7 dias', days: 7 },
  '30d': { label: '30 dias', days: 30 },
  '90d': { label: '90 dias', days: 90 },
};

export function buildDateRange(preset: PeriodPreset, customFrom?: Date, customTo?: Date) {
  const to = new Date();
  to.setHours(23, 59, 59, 999);

  if (preset === 'custom' && customFrom && customTo) {
    return { from: customFrom, to: customTo };
  }

  const days = PERIOD_PRESETS[preset as keyof typeof PERIOD_PRESETS]?.days ?? 30;
  const from = new Date();
  from.setDate(from.getDate() - days);
  from.setHours(0, 0, 0, 0);
  return { from, to };
}
