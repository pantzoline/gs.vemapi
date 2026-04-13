'use client';

import Link from 'next/link';
import { useAuth } from '@/contexts/AuthContext';
import { NotificationBell } from '@/components/layout/NotificationBell';
import { useNotifications } from '@/providers/SignalRProvider';
import {
  LayoutDashboard, FileText, Users, CreditCard,
  LogOut, ChevronDown, Shield
} from 'lucide-react';
import { useState } from 'react';
import { cn } from '@/lib/utils';

const NAV_LINKS = [
  { href: '/dashboard',                icon: LayoutDashboard, label: 'Dashboard'     },
  { href: '/dashboard/orders',         icon: FileText,         label: 'Pedidos'       },
  { href: '/dashboard/billing',        icon: CreditCard,       label: 'Financeiro'   },
  { href: '/dashboard/partners',       icon: Users,            label: 'Parceiros'    },
];

export function AppHeader() {
  const { user, logout, isMaster, isAdminAr } = useAuth();
  const [userMenuOpen, setUserMenuOpen] = useState(false);

  if (!user) return null;

  return (
    <header className="sticky top-0 z-40 w-full border-b border-gray-800/60
                        bg-gray-950/90 backdrop-blur-md">
      <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex h-14 items-center justify-between gap-4">

          {/* Logo + Nav */}
          <div className="flex items-center gap-6">
            <Link href="/dashboard"
              className="flex items-center gap-2 font-bold text-white">
              <Shield className="w-5 h-5 text-blue-400" />
              <span className="hidden sm:block">CoreAr</span>
            </Link>

            <nav className="hidden md:flex items-center gap-1">
              {NAV_LINKS.map(({ href, icon: Icon, label }) => (
                <Link key={href} href={href}
                  className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg
                             text-gray-400 hover:text-white hover:bg-gray-800/60
                             text-sm font-medium transition-all">
                  <Icon className="w-4 h-4" />
                  {label}
                </Link>
              ))}
            </nav>
          </div>

          {/* Direita: Sininho + User Menu */}
          <div className="flex items-center gap-2">
            {/* ★ SININHO DE NOTIFICAÇÕES ★ */}
            <NotificationBell />

            {/* Divider */}
            <div className="w-px h-6 bg-gray-700" />

            {/* Menu do usuário */}
            <div className="relative">
              <button
                onClick={() => setUserMenuOpen(v => !v)}
                className="flex items-center gap-2 px-2 py-1.5 rounded-lg
                           hover:bg-gray-800 transition-all"
              >
                {/* Avatar com inicial */}
                <div className="w-7 h-7 rounded-lg bg-gradient-to-br from-blue-500 to-violet-600
                                flex items-center justify-center text-xs font-bold text-white">
                  {user.fullName.charAt(0).toUpperCase()}
                </div>
                <div className="hidden sm:block text-left">
                  <p className="text-gray-200 text-xs font-medium leading-tight max-w-28 truncate">
                    {user.fullName}
                  </p>
                  <p className="text-gray-500 text-[10px] leading-tight">
                    {isMaster ? 'Master' : isAdminAr ? 'Admin AR' : user.tenantLevel}
                  </p>
                </div>
                <ChevronDown className={cn(
                  'w-3 h-3 text-gray-500 transition-transform duration-200',
                  userMenuOpen && 'rotate-180'
                )} />
              </button>

              {userMenuOpen && (
                <div className="absolute right-0 mt-1 w-48 bg-gray-900 border border-gray-700/60
                                rounded-xl shadow-2xl animate-in fade-in slide-in-from-top-2
                                duration-150 z-50 overflow-hidden">
                  <div className="px-3 py-2.5 border-b border-gray-700/40">
                    <p className="text-gray-200 text-sm font-medium truncate">{user.fullName}</p>
                    <p className="text-gray-500 text-xs truncate">{user.email}</p>
                  </div>
                  <div className="p-1">
                    <button
                      onClick={logout}
                      className="w-full flex items-center gap-2 px-3 py-2 rounded-lg
                                 text-gray-400 hover:text-red-400 hover:bg-red-900/20
                                 text-sm transition-all"
                    >
                      <LogOut className="w-4 h-4" />
                      Sair
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}
