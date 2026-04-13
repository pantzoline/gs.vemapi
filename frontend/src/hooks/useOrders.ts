import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiClient } from '@/lib/api-client';
import type { OrderDetail, OrderListQuery, OrderListResult, OrderStatus } from '@/types/order.types';

const ORDERS_KEY = 'orders';

// ─── Listagem paginada ────────────────────────────────────────────────────────
export function useOrders(query: OrderListQuery) {
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
    sortDesc: String(query.sortDesc ?? true),
    ...(query.search && { search: query.search }),
    ...(query.sortBy && { sortBy: query.sortBy }),
    ...(query.from && { from: query.from }),
    ...(query.to && { to: query.to }),
    ...(query.statuses?.length && { statuses: query.statuses.join(',') }),
    ...(query.acProviders?.length && { acProviders: query.acProviders.join(',') }),
    ...(query.certTypes?.length && { certTypes: query.certTypes.join(',') }),
  });

  return useQuery({
    queryKey: [ORDERS_KEY, 'list', query],
    queryFn: () => apiClient.get<OrderListResult>(`/api/orders?${params}`),
    staleTime: 30_000, // 30s — dados de lista mudam com mais frequência
    placeholderData: (prev) => prev, // Mantém dados anteriores durante refetch (anti-flicker)
  });
}

// ─── Detalhes sem consulta AC ─────────────────────────────────────────────────
export function useOrderDetail(orderId: string | null) {
  return useQuery({
    queryKey: [ORDERS_KEY, 'detail', orderId],
    queryFn: () => apiClient.get<OrderDetail>(`/api/orders/${orderId}`),
    enabled: !!orderId,
    staleTime: 60_000,
  });
}

// ─── Detalhes com status em tempo real da AC ──────────────────────────────────
export function useOrderDetailLive(orderId: string | null) {
  return useQuery({
    queryKey: [ORDERS_KEY, 'detail-live', orderId],
    queryFn: () => apiClient.get<OrderDetail>(`/api/orders/${orderId}/live`),
    enabled: !!orderId,
    // Polling a cada 30s enquanto o pedido está em processamento na AC
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      const activeStatuses: OrderStatus[] = ['IssuingAtAc', 'AwaitingVideoConference'];
      return status && activeStatuses.includes(status) ? 30_000 : false;
    },
  });
}

// ─── Transição de Status ──────────────────────────────────────────────────────
export function useTransitionStatus(orderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ status, description }: { status: OrderStatus; description: string }) =>
      apiClient.post(`/api/orders/${orderId}/transition`, { status, description }),
    onSuccess: () => {
      // Invalida o cache do pedido para recarregar timeline atualizada
      qc.invalidateQueries({ queryKey: [ORDERS_KEY, 'detail', orderId] });
      qc.invalidateQueries({ queryKey: [ORDERS_KEY, 'list'] });
    },
  });
}

// ─── Reenvio de Link de Pagamento ─────────────────────────────────────────────
export function useResendPaymentLink(orderId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => apiClient.post(`/api/orders/${orderId}/resend-payment-link`),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: [ORDERS_KEY, 'detail', orderId] });
    },
  });
}
