'use client';

import { useQuery } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useAuth } from '@/contexts/AuthContext';
import { apiClient } from '@/lib/api-client';
import type { TenantBranding, TenantHierarchy } from '@/types/management.types';

// ─── Hook: Identidade Visual do Tenant ────────────────────────────────────────
/**
 * Carrega o branding do tenant logado e aplica as CSS Variables globalmente.
 * Deve ser usado UMA VEZ no layout do dashboard (app/dashboard/layout.tsx).
 *
 * Efeito: as classes Tailwind com `text-[var(--color-primary)]` funcionam
 * automaticamente com as cores do parceiro logado.
 */
export function useTenantBranding() {
  const { user } = useAuth();

  const query = useQuery({
    queryKey: ['tenant', 'branding', user?.tenantId],
    queryFn: () => apiClient.get<TenantBranding>(`/api/management/tenants/${user?.tenantId}/branding`),
    enabled: !!user?.tenantId,
    staleTime: 10 * 60 * 1000, // 10 min — cores mudam raramente
    gcTime: 30 * 60 * 1000,
  });

  // Aplica as CSS Variables no :root quando os dados chegam
  useEffect(() => {
    if (!query.data) return;

    const root = document.documentElement;
    const { primaryColor, secondaryColor, accentColor } = query.data;

    // Converte HEX → RGB para uso com opacidade: rgb(var(--color-primary) / 0.1)
    const hexToRgb = (hex: string) => {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return `${r} ${g} ${b}`;
    };

    root.style.setProperty('--color-primary',   hexToRgb(primaryColor));
    root.style.setProperty('--color-secondary', hexToRgb(secondaryColor));
    root.style.setProperty('--color-accent',    hexToRgb(accentColor));

    // Para uso direto como HEX (ícones, borders, etc.)
    root.style.setProperty('--color-primary-hex',   primaryColor);
    root.style.setProperty('--color-secondary-hex', secondaryColor);
    root.style.setProperty('--color-accent-hex',    accentColor);
  }, [query.data]);

  return query;
}

// ─── Hook: Hierarquia de Tenants ──────────────────────────────────────────────
export function useTenantHierarchy() {
  return useQuery({
    queryKey: ['tenant', 'hierarchy'],
    queryFn: () => apiClient.get<TenantHierarchy>('/api/management/tenants/hierarchy'),
    staleTime: 2 * 60 * 1000,
  });
}

// ─── Hook: Detalhes de um Tenant ─────────────────────────────────────────────
export function useTenantDetail(tenantId: string | null) {
  return useQuery({
    queryKey: ['tenant', 'detail', tenantId],
    queryFn: () => apiClient.get<TenantHierarchy>(`/api/management/tenants/${tenantId}`),
    enabled: !!tenantId,
  });
}
