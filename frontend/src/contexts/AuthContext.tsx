'use client';

import React, { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { apiClient, login as apiLogin, logout as apiLogout, getAccessToken } from '@/lib/api-client';

// ─── Tipos ────────────────────────────────────────────────────────────────────
type TenantLevel = 'Master' | 'AuthorityRegistrar' | 'PointOfAttendance' | 'Agent';

type AuthUser = {
  id: string;
  fullName: string;
  email: string;
  tenantId: string;
  tenantLevel: TenantLevel;
  roles: string[];
};

type AuthContextValue = {
  user: AuthUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  // Helpers de Role para uso em componentes (no-op se não autenticado)
  isMaster: boolean;
  isAdminAr: boolean;
  isPa: boolean;
  isAgent: boolean;
  hasRole: (...roles: string[]) => boolean;
};

// ─── Context ──────────────────────────────────────────────────────────────────
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Carrega o usuário atual ao montar (verifica se há token válido)
  useEffect(() => {
    const initializeAuth = async () => {
      const token = getAccessToken();
      if (!token) {
        setIsLoading(false);
        return;
      }

      try {
        // GET /api/auth/me lê direto dos Claims do JWT — sem query no banco
        const me = await apiClient.get<AuthUser>('/api/auth/me');
        setUser(me);
      } catch {
        // Token inválido ou expirado sem refresh disponível
        setUser(null);
      } finally {
        setIsLoading(false);
      }
    };

    initializeAuth();
  }, []);

  const handleLogin = useCallback(async (email: string, password: string) => {
    const response = await apiLogin(email, password);
    const me = await apiClient.get<AuthUser>('/api/auth/me');
    setUser(me);
  }, []);

  const handleLogout = useCallback(async () => {
    await apiLogout();
    setUser(null);
  }, []);

  const hasRole = useCallback((...roles: string[]) => {
    if (!user) return false;
    return roles.some(r => user.roles.includes(r));
  }, [user]);

  const value: AuthContextValue = {
    user,
    isAuthenticated: !!user,
    isLoading,
    login: handleLogin,
    logout: handleLogout,
    isMaster:  hasRole('ROLE_MASTER'),
    isAdminAr: hasRole('ROLE_ADMIN_AR'),
    isPa:      hasRole('ROLE_PA'),
    isAgent:   hasRole('ROLE_AGR'),
    hasRole,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ─── Hook principal ───────────────────────────────────────────────────────────
export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth deve ser usado dentro de <AuthProvider>');
  return ctx;
}

// ─── Hook de guard para componentes ──────────────────────────────────────────
/**
 * Uso: const { isAllowed } = useRoleGuard('ROLE_MASTER', 'ROLE_ADMIN_AR');
 * Renderiza null se não tem acesso, ou exibe o componente.
 */
export function useRoleGuard(...allowedRoles: string[]) {
  const { hasRole, isAuthenticated, isLoading } = useAuth();
  return {
    isLoading,
    isAllowed: isAuthenticated && (allowedRoles.length === 0 || hasRole(...allowedRoles)),
  };
}
