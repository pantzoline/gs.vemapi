import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

/**
 * Next.js Middleware — Proteção de Rotas Server-Side.
 *
 * Executado no Edge Runtime ANTES de qualquer página carregar.
 * Redireciona usuários não autenticados para o login e usuários sem
 * a Role correta para uma página 403.
 *
 * O Access Token é armazenado em um cookie acessível via JS chamado
 * "access_token" (HttpOnly=false para o middleware poder lê-lo).
 * O Refresh Token está em "X-Refresh-Token" (HttpOnly=true, apenas o backend acessa).
 */

// Mapa de rotas para roles permitidas (null = qualquer autenticado)
const ROUTE_ROLE_MAP: Record<string, string[] | null> = {
  '/dashboard/admin':   ['ROLE_MASTER'],
  '/dashboard/ar':      ['ROLE_MASTER', 'ROLE_ADMIN_AR'],
  '/dashboard/pa':      ['ROLE_MASTER', 'ROLE_ADMIN_AR', 'ROLE_PA'],
  '/dashboard/orders':  null, // Qualquer autenticado
  '/dashboard/billing': ['ROLE_MASTER', 'ROLE_ADMIN_AR'],
};

// Rotas públicas — nunca verificar auth
const PUBLIC_ROUTES = ['/login', '/register', '/forgot-password', '/api/auth'];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Ignora rotas públicas e assets estáticos
  if (PUBLIC_ROUTES.some(r => pathname.startsWith(r)) ||
      pathname.startsWith('/_next') ||
      pathname.includes('.')) {
    return NextResponse.next();
  }

  // Lê o Access Token do cookie (cookie não-HttpOnly para o middleware Edge)
  const accessToken = request.cookies.get('access_token')?.value;

  if (!accessToken) {
    // Preserva a URL original para redirecionar após login
    const loginUrl = new URL('/login', request.url);
    loginUrl.searchParams.set('callbackUrl', encodeURIComponent(pathname));
    return NextResponse.redirect(loginUrl);
  }

  // Decodifica o JWT sem verificar assinatura (verificação é do backend)
  // O middleware apenas lê os claims para roteamento — não confiar sem verificação
  const payload = decodeJwtPayload(accessToken);

  if (!payload) {
    // Token malformado — limpa cookies e redireciona
    const response = NextResponse.redirect(new URL('/login', request.url));
    response.cookies.delete('access_token');
    return response;
  }

  // Verifica se o token está expirado (exp claim em segundos Unix)
  const isExpired = payload.exp ? payload.exp < Math.floor(Date.now() / 1000) : true;
  if (isExpired) {
    // Token expirado → redireciona para rota especial que tenta o refresh silencioso
    const refreshUrl = new URL('/api/auth/silent-refresh', request.url);
    refreshUrl.searchParams.set('callbackUrl', encodeURIComponent(pathname));
    return NextResponse.redirect(refreshUrl);
  }

  // ─── RBAC: Verifica se a role permite acesso à rota ──────────────────────
  const requiredRoles = getRequiredRoles(pathname);
  if (requiredRoles !== undefined) {
    const userRoles: string[] = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? payload.role // compatibilidade com variações do claim name
      ?? [];

    const rolesArray = Array.isArray(userRoles) ? userRoles : [userRoles];
    const hasAccess = requiredRoles === null ||
                      rolesArray.some(r => requiredRoles.includes(r));

    if (!hasAccess) {
      return NextResponse.redirect(new URL('/403', request.url));
    }
  }

  // Injeta headers úteis para Server Components lerem sem re-decodificar
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set('x-user-id', payload.sub ?? '');
  requestHeaders.set('x-tenant-id', payload.tenant_id ?? '');
  requestHeaders.set('x-tenant-level', payload.tenant_level ?? '');

  return NextResponse.next({ request: { headers: requestHeaders } });
}

// ─── Utilitários ────────────────────────────────────────────────────────────
function decodeJwtPayload(token: string): Record<string, any> | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const payload = Buffer.from(parts[1], 'base64url').toString('utf8');
    return JSON.parse(payload);
  } catch {
    return null;
  }
}

function getRequiredRoles(pathname: string): string[] | null | undefined {
  // Verifica correspondência exata e depois por prefixo
  for (const [route, roles] of Object.entries(ROUTE_ROLE_MAP)) {
    if (pathname.startsWith(route)) return roles;
  }
  return null; // Qualquer autenticado
}

// Define quais caminhos o middleware intercepta
export const config = {
  matcher: [
    '/((?!_next/static|_next/image|favicon.ico).*)',
  ],
};
