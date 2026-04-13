/**
 * API Client com interceptor de Refresh Token automático.
 *
 * Fluxo de Renovação Silenciosa:
 * 1. Request falha com 401 Unauthorized
 * 2. Interceptor chama POST /api/auth/refresh-token (o cookie HttpOnly vai automaticamente)
 * 3. Backend rota o refresh token e retorna novo Access Token
 * 4. Cliente salva o novo token e REPETE a request original
 * 5. Usuário não percebe nada — zero interrupção
 *
 * Se o refresh também falhar (token expirado/revogado), redireciona para /login.
 */

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5001';
const TOKEN_COOKIE  = 'access_token';

// ─── Gerenciamento do Token em Memória ──────────────────────────────────────
// O Access Token fica em memória (mais seguro que localStorage) e em cookie
// não-HttpOnly para o Middleware do Next.js poder ler.
let inMemoryToken: string | null = null;
let isRefreshing = false;
// Fila de requests que chegaram durante o refresh — serão reexecutadas juntas
let refreshSubscribers: Array<(token: string) => void> = [];

function subscribeToRefresh(callback: (token: string) => void) {
  refreshSubscribers.push(callback);
}

function notifyRefreshSubscribers(newToken: string) {
  refreshSubscribers.forEach(cb => cb(newToken));
  refreshSubscribers = [];
}

// Salva o token em memória e em cookie (para o middleware ler)
function saveAccessToken(token: string) {
  inMemoryToken = token;
  // Cookie não-HttpOnly para o middleware Edge poder decodificar
  document.cookie = `${TOKEN_COOKIE}=${token}; path=/; SameSite=Strict; Secure`;
}

function clearAccessToken() {
  inMemoryToken = null;
  document.cookie = `${TOKEN_COOKIE}=; path=/; max-age=0`;
}

export function getAccessToken(): string | null {
  return inMemoryToken;
}

// ─── Função Base de Fetch com Interceptor ────────────────────────────────────
async function apiFetch<T>(
  endpoint: string,
  options: RequestInit = {}
): Promise<T> {
  const token = getAccessToken();

  const headers: HeadersInit = {
    'Content-Type': 'application/json',
    ...options.headers,
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
  };

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers,
    credentials: 'include', // Envia cookies HttpOnly (Refresh Token) automaticamente
  });

  // ─── Tratamento do 401: Tentativa de Refresh Silencioso ──────────────────
  if (response.status === 401) {
    // Se já está em processo de refresh, adiciona à fila e aguarda
    if (isRefreshing) {
      return new Promise<T>((resolve, reject) => {
        subscribeToRefresh(async (newToken) => {
          try {
            const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, {
              ...options,
              headers: { ...headers, Authorization: `Bearer ${newToken}` },
              credentials: 'include',
            });
            resolve(await retryResponse.json());
          } catch (error) {
            reject(error);
          }
        });
      });
    }

    isRefreshing = true;

    try {
      // Chama o refresh — o HttpOnly cookie vai automaticamente (credentials: include)
      const refreshResponse = await fetch(`${API_BASE_URL}/api/auth/refresh-token`, {
        method: 'POST',
        credentials: 'include',
      });

      if (!refreshResponse.ok) {
        // Refresh falhou — sessão expirada
        clearAccessToken();
        isRefreshing = false;
        // Redireciona para login preservando a URL atual
        window.location.href = `/login?callbackUrl=${encodeURIComponent(window.location.pathname)}`;
        throw new Error('Sessão expirada. Redirecionando para login.');
      }

      const { accessToken: newToken } = await refreshResponse.json();
      saveAccessToken(newToken);
      isRefreshing = false;

      // Notifica todas as requests que estavam aguardando
      notifyRefreshSubscribers(newToken);

      // Repete a request original com o novo token
      const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, {
        ...options,
        headers: { ...headers, Authorization: `Bearer ${newToken}` },
        credentials: 'include',
      });

      return retryResponse.json();
    } catch (error) {
      isRefreshing = false;
      throw error;
    }
  }

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({ message: 'Erro desconhecido.' }));
    throw { status: response.status, data: errorData };
  }

  // 204 No Content não tem body
  if (response.status === 204) return undefined as T;

  return response.json();
}

// ─── API Client com métodos tipados ──────────────────────────────────────────
export const apiClient = {
  get: <T>(url: string, options?: RequestInit) =>
    apiFetch<T>(url, { method: 'GET', ...options }),

  post: <T>(url: string, body?: unknown, options?: RequestInit) =>
    apiFetch<T>(url, { method: 'POST', body: JSON.stringify(body), ...options }),

  put: <T>(url: string, body?: unknown, options?: RequestInit) =>
    apiFetch<T>(url, { method: 'PUT', body: JSON.stringify(body), ...options }),

  patch: <T>(url: string, body?: unknown, options?: RequestInit) =>
    apiFetch<T>(url, { method: 'PATCH', body: JSON.stringify(body), ...options }),

  delete: <T>(url: string, options?: RequestInit) =>
    apiFetch<T>(url, { method: 'DELETE', ...options }),
};

// ─── Funções de Auth ──────────────────────────────────────────────────────────
export async function login(email: string, password: string) {
  const response = await apiClient.post<{
    accessToken: string;
    expiresIn: number;
    user: { id: string; fullName: string; email: string; tenantId: string; tenantLevel: string; };
  }>('/api/auth/login', { email, password });

  saveAccessToken(response.accessToken);
  return response;
}

export async function logout() {
  await apiClient.post('/api/auth/logout').catch(() => {});
  clearAccessToken();
  window.location.href = '/login';
}
