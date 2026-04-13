'use client';

import type { KpiCard, TrendDirection } from '@/types/dashboard.types';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';
import { ReactNode } from 'react';

// ─── Ícones por direção ───────────────────────────────────────────────────────
function TrendBadge({ trend, change }: { trend: TrendDirection; change: number | null }) {
  if (change === null) return null;

  const configs = {
    Up:      { icon: TrendingUp,   color: 'text-emerald-400', bg: 'bg-emerald-400/10', sign: '+' },
    Down:    { icon: TrendingDown, color: 'text-red-400',     bg: 'bg-red-400/10',     sign: '' },
    Neutral: { icon: Minus,        color: 'text-gray-400',    bg: 'bg-gray-400/10',    sign: '' },
  };

  const { icon: Icon, color, bg, sign } = configs[trend];

  return (
    <span className={`flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full ${color} ${bg}`}>
      <Icon className="w-3 h-3" />
      {sign}{Math.abs(change).toFixed(1)}%
    </span>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────
export function KpiCardSkeleton() {
  return (
    <div className="animate-pulse p-5 rounded-2xl bg-gray-800/60 border border-gray-700/40">
      <div className="flex justify-between items-start mb-4">
        <div className="h-3 w-28 bg-gray-700 rounded" />
        <div className="h-5 w-16 bg-gray-700 rounded-full" />
      </div>
      <div className="h-8 w-36 bg-gray-700 rounded mb-2" />
      <div className="h-3 w-24 bg-gray-700/60 rounded" />
    </div>
  );
}

// ─── Card Principal ───────────────────────────────────────────────────────────
type KpiCardComponentProps = {
  data: KpiCard;
  icon: ReactNode;
  accentColor?: string; // ex: "blue" | "emerald" | "violet" | "amber"
};

const colorMap: Record<string, { border: string; iconBg: string; glow: string }> = {
  blue:    { border: 'border-blue-500/20',   iconBg: 'bg-blue-500/10',   glow: 'shadow-blue-500/10' },
  emerald: { border: 'border-emerald-500/20', iconBg: 'bg-emerald-500/10', glow: 'shadow-emerald-500/10' },
  violet:  { border: 'border-violet-500/20', iconBg: 'bg-violet-500/10', glow: 'shadow-violet-500/10' },
  amber:   { border: 'border-amber-500/20',  iconBg: 'bg-amber-500/10',  glow: 'shadow-amber-500/10' },
};

export function KpiCardWidget({ data, icon, accentColor = 'blue' }: KpiCardComponentProps) {
  const colors = colorMap[accentColor] ?? colorMap.blue;

  return (
    <div className={`relative overflow-hidden p-5 rounded-2xl
                     bg-gray-800/60 border ${colors.border}
                     shadow-xl ${colors.glow}
                     backdrop-blur-sm
                     hover:bg-gray-800/80 transition-all duration-300
                     group`}>
      {/* Glow sutil no canto superior direito */}
      <div className={`absolute -top-6 -right-6 w-24 h-24 rounded-full blur-2xl opacity-20
                      ${accentColor === 'blue' ? 'bg-blue-500' :
                        accentColor === 'emerald' ? 'bg-emerald-500' :
                        accentColor === 'violet' ? 'bg-violet-500' : 'bg-amber-500'}`}
      />

      <div className="relative">
        {/* Header: Label + Badge de Tendência */}
        <div className="flex items-start justify-between mb-3">
          <div className={`p-2 rounded-xl ${colors.iconBg} group-hover:scale-110 transition-transform`}>
            {icon}
          </div>
          <TrendBadge trend={data.trend} change={data.changePercent} />
        </div>

        {/* Valor principal */}
        <p className="text-2xl font-bold text-white tracking-tight leading-none mb-1">
          {data.formattedValue}
        </p>

        {/* Label */}
        <p className="text-gray-400 text-sm font-medium">{data.label}</p>

        {/* Comparação com período anterior */}
        {data.changePercent !== null && (
          <p className="text-gray-600 text-xs mt-2">
            vs. período anterior
          </p>
        )}
      </div>
    </div>
  );
}
