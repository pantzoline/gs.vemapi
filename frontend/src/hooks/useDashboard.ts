import { useQuery } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';
import type { DashboardSummary, DashboardCharts, DashboardFilter } from '@/types/dashboard.types';

// ─── Funções de fetch tipadas ─────────────────────────────────────────────────
function buildParams(filter: DashboardFilter): Record<string, string> {
  return {
    from: filter.from.toISOString(),
    to:   filter.to.toISOString(),
  };
}

const fetchSummary = (filter: DashboardFilter) =>
  apiClient.get<DashboardSummary>(
    `/api/dashboard/summary?${new URLSearchParams(buildParams(filter))}`
  );

const fetchCharts = (filter: DashboardFilter) =>
  apiClient.get<DashboardCharts>(
    `/api/dashboard/charts?${new URLSearchParams(buildParams(filter))}`
  );

// ─── Hook: KPIs do topo ─────────────────────────────────────────────────────
export function useDashboardSummary(filter: DashboardFilter) {
  return useQuery({
    // A chave inclui o período — muda o período, invalida o cache
    queryKey: ['dashboard', 'summary', filter.from.toISOString(), filter.to.toISOString()],
    queryFn:  () => fetchSummary(filter),
    staleTime: 5 * 60 * 1000,    // 5 min: dados de summary são estáveis
    retry: 2,
  });
}

// ─── Hook: Dados dos gráficos ────────────────────────────────────────────────
export function useDashboardCharts(filter: DashboardFilter) {
  return useQuery({
    queryKey: ['dashboard', 'charts', filter.from.toISOString(), filter.to.toISOString()],
    queryFn:  () => fetchCharts(filter),
    staleTime: 5 * 60 * 1000,
    retry: 2,
  });
}
