'use client';

import { useState } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { useDashboardSummary, useDashboardCharts } from '@/hooks/useDashboard';
import { PeriodFilter } from '@/components/dashboard/PeriodFilter';
import { KpiCardWidget, KpiCardSkeleton } from '@/components/dashboard/KpiCardWidget';
import { SalesAreaChart, SalesChartSkeleton } from '@/components/charts/SalesAreaChart';
import { OrderStatusPieChart } from '@/components/charts/OrderStatusPieChart';
import { PartnerRankingChart } from '@/components/charts/PartnerRankingChart';
import type { DashboardFilter } from '@/types/dashboard.types';
import { buildDateRange } from '@/types/dashboard.types';
import {
  DollarSign, TrendingUp, CreditCard, CheckCircle2,
  RefreshCw, LayoutDashboard
} from 'lucide-react';

// ─── Container reutilizável para os cards de gráfico ─────────────────────────
function ChartCard({
  title,
  subtitle,
  children,
  className = '',
}: {
  title: string;
  subtitle?: string;
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={`bg-gray-800/60 border border-gray-700/40 rounded-2xl p-5
                     backdrop-blur-sm shadow-xl ${className}`}>
      <div className="mb-4">
        <h3 className="text-gray-100 font-semibold text-sm">{title}</h3>
        {subtitle && <p className="text-gray-500 text-xs mt-0.5">{subtitle}</p>}
      </div>
      {children}
    </div>
  );
}

// ─── Página Principal ─────────────────────────────────────────────────────────
export default function DashboardPage() {
  const { user, isMaster, isAdminAr } = useAuth();

  // Estado global do filtro de período — alimenta TODOS os widgets simultaneamente
  const [filter, setFilter] = useState<DashboardFilter>(() => {
    const { from, to } = buildDateRange('30d');
    return { preset: '30d', from, to };
  });

  // ─── Queries (cada uma tem seu cache independente por período) ────────────
  const {
    data: summary,
    isLoading: summaryLoading,
    isError: summaryError,
    refetch: refetchSummary
  } = useDashboardSummary(filter);

  const {
    data: charts,
    isLoading: chartsLoading,
    isError: chartsError,
    refetch: refetchCharts
  } = useDashboardCharts(filter);

  const isLoading = summaryLoading || chartsLoading;

  const handleRefresh = () => {
    refetchSummary();
    refetchCharts();
  };

  // ─── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="min-h-screen bg-gray-950 text-white">

      {/* ── Fundo com gradiente sutil ─────────────────────────────────────── */}
      <div className="fixed inset-0 pointer-events-none">
        <div className="absolute top-0 left-1/4 w-96 h-96 bg-blue-600/5 rounded-full blur-3xl" />
        <div className="absolute bottom-1/4 right-1/4 w-80 h-80 bg-violet-600/5 rounded-full blur-3xl" />
      </div>

      <div className="relative px-4 sm:px-6 lg:px-8 py-6 max-w-screen-2xl mx-auto space-y-6">

        {/* ── Header ───────────────────────────────────────────────────────── */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-2 mb-1">
              <LayoutDashboard className="w-5 h-5 text-blue-400" />
              <h1 className="text-xl font-bold text-white">Dashboard</h1>
              {/* Badge do nível do usuário */}
              <span className="px-2 py-0.5 rounded-full text-xs font-medium
                               bg-blue-500/10 text-blue-400 border border-blue-500/20">
                {summary?.tenantLevel ?? user?.tenantLevel ?? '...'}
              </span>
            </div>
            <p className="text-gray-500 text-sm">
              {summary?.tenantName
                ? `Dados de ${summary.tenantName}`
                : user?.fullName ?? 'Carregando...'}
            </p>
          </div>

          {/* Controles: Filtro de Período + Refresh */}
          <div className="flex items-center gap-3">
            <PeriodFilter value={filter} onChange={setFilter} />
            <button
              onClick={handleRefresh}
              disabled={isLoading}
              className="p-2 rounded-xl bg-gray-800 border border-gray-700 text-gray-400
                         hover:text-white hover:border-gray-600 transition-all
                         disabled:opacity-40 disabled:cursor-not-allowed"
              title="Atualizar dados"
            >
              <RefreshCw className={`w-4 h-4 ${isLoading ? 'animate-spin' : ''}`} />
            </button>
          </div>
        </div>

        {/* ── Erro Global ───────────────────────────────────────────────────── */}
        {(summaryError || chartsError) && (
          <div className="flex items-center gap-3 p-4 bg-red-900/20 border border-red-700/40
                          rounded-xl text-red-400 text-sm">
            <span>⚠</span>
            <span>Falha ao carregar dados. Verifique sua conexão e tente novamente.</span>
            <button onClick={handleRefresh}
              className="ml-auto underline hover:no-underline">
              Tentar novamente
            </button>
          </div>
        )}

        {/* ── KPIs: Grid 1→2→4 colunas ─────────────────────────────────────── */}
        <div className="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4">
          {summaryLoading ? (
            <>
              <KpiCardSkeleton />
              <KpiCardSkeleton />
              <KpiCardSkeleton />
              <KpiCardSkeleton />
            </>
          ) : summary ? (
            <>
              <KpiCardWidget
                data={summary.totalSales}
                accentColor="blue"
                icon={<DollarSign className="w-4 h-4 text-blue-400" />}
              />
              <KpiCardWidget
                data={summary.netRevenue}
                accentColor="emerald"
                icon={<TrendingUp className="w-4 h-4 text-emerald-400" />}
              />
              <KpiCardWidget
                data={summary.averageTicket}
                accentColor="violet"
                icon={<CreditCard className="w-4 h-4 text-violet-400" />}
              />
              <KpiCardWidget
                data={summary.protocolConversion}
                accentColor="amber"
                icon={<CheckCircle2 className="w-4 h-4 text-amber-400" />}
              />
            </>
          ) : null}
        </div>

        {/* ── Gráfico de Área: Tendência de Vendas (largura total) ─────────── */}
        <ChartCard
          title="Tendência de Vendas"
          subtitle="Receita bruta e líquida no período selecionado"
        >
          {chartsLoading
            ? <SalesChartSkeleton />
            : <SalesAreaChart data={charts?.salesTimeSeries ?? []} />
          }
        </ChartCard>

        {/* ── Segunda Linha: Donut + Ranking (visível apenas para Master/AR) ── */}
        <div className={`grid gap-4 ${
          isMaster || isAdminAr ? 'grid-cols-1 lg:grid-cols-2' : 'grid-cols-1'
        }`}>
          {/* Distribuição por Status — todos os níveis veem */}
          <ChartCard
            title="Status dos Pedidos"
            subtitle="Distribuição por etapa do fluxo"
          >
            <OrderStatusPieChart
              data={charts?.statusDistribution ?? []}
              isLoading={chartsLoading}
            />
          </ChartCard>

          {/* Ranking de Parceiros — APENAS Master e AdminAR */}
          {/* Backend retorna null p/ PA; frontend esconde pelo role também */}
          {(isMaster || isAdminAr) && (
            <ChartCard
              title="Ranking de Parceiros"
              subtitle="Top 5 PAs por receita no período"
            >
              <PartnerRankingChart
                data={charts?.partnerRanking ?? []}
                isLoading={chartsLoading}
              />
            </ChartCard>
          )}
        </div>

        {/* ── Receita por Produto ───────────────────────────────────────────── */}
        {charts?.revenueByProduct?.length ? (
          <ChartCard
            title="Performance por Produto"
            subtitle="Receita e volume por tipo de certificado"
          >
            <div className="space-y-2">
              {charts.revenueByProduct.map((product, i) => {
                const maxRevenue = charts.revenueByProduct[0].revenue;
                const pct = Math.round((product.revenue / maxRevenue) * 100);
                return (
                  <div key={product.productCode} className="flex items-center gap-3">
                    <span className="text-gray-500 text-xs w-4 text-right">{i + 1}</span>
                    <div className="flex-1">
                      <div className="flex justify-between text-xs mb-1">
                        <span className="text-gray-300 font-medium truncate">{product.productName}</span>
                        <div className="flex items-center gap-3 shrink-0 ml-2">
                          <span className="text-gray-500">{product.quantity}x</span>
                          <span className="text-white font-semibold">
                            {new Intl.NumberFormat('pt-BR', {
                              style: 'currency', currency: 'BRL', notation: 'compact'
                            }).format(product.revenue)}
                          </span>
                        </div>
                      </div>
                      <div className="h-1 bg-gray-700 rounded-full overflow-hidden">
                        <div
                          className="h-full bg-gradient-to-r from-blue-500 to-violet-500 rounded-full
                                     transition-all duration-700"
                          style={{ width: `${pct}%` }}
                        />
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          </ChartCard>
        ) : null}

      </div>
    </div>
  );
}
