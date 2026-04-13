'use client';

import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  Tooltip, ResponsiveContainer, Cell
} from 'recharts';
import type { PartnerRanking } from '@/types/dashboard.types';
import { TrendingUp, TrendingDown, Minus, Medal } from 'lucide-react';

const formatBrl = (v: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL',
    notation: 'compact', maximumFractionDigits: 1 }).format(v);

const MEDAL_COLORS = ['#f59e0b', '#9ca3af', '#cd7f32', '#6366f1', '#6366f1'];

function RankTooltip({ active, payload }: any) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload as PartnerRanking;

  return (
    <div className="bg-gray-900/95 border border-gray-700 rounded-xl p-3 shadow-2xl backdrop-blur-sm">
      <p className="text-white font-semibold text-sm mb-1">{d.paName}</p>
      <p className="text-gray-400 text-xs mb-2">AR: {d.arName}</p>
      <div className="space-y-1">
        <div className="flex justify-between gap-6 text-xs">
          <span className="text-gray-400">Receita</span>
          <span className="text-white font-semibold">
            {new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(d.totalRevenue)}
          </span>
        </div>
        <div className="flex justify-between gap-6 text-xs">
          <span className="text-gray-400">Pedidos</span>
          <span className="text-white font-semibold">{d.totalOrders}</span>
        </div>
      </div>
    </div>
  );
}

export function PartnerRankingChart({
  data,
  isLoading
}: {
  data: PartnerRanking[];
  isLoading?: boolean;
}) {
  if (isLoading) {
    return (
      <div className="animate-pulse space-y-3">
        {[...Array(5)].map((_, i) => (
          <div key={i} className="flex items-center gap-3">
            <div className="w-6 h-6 rounded-full bg-gray-700 shrink-0" />
            <div className="flex-1 h-8 bg-gray-700 rounded" style={{ width: `${90 - i * 12}%` }} />
          </div>
        ))}
      </div>
    );
  }

  if (!data.length) {
    return (
      <div className="flex flex-col items-center justify-center h-48 gap-2">
        <Medal className="w-10 h-10 text-gray-600" />
        <p className="text-gray-500 text-sm">Nenhum parceiro com dados no período</p>
      </div>
    );
  }

  return (
    <div className="space-y-1">
      {/* Lista com barras inline (mais legível que BarChart puro para ranking) */}
      {data.map((partner, i) => {
        const maxRevenue = data[0].totalRevenue;
        const widthPct = Math.round((partner.totalRevenue / maxRevenue) * 100);

        return (
          <div key={partner.paId}
            className="group flex items-center gap-3 p-2 rounded-lg hover:bg-white/5
                       transition-colors cursor-default">
            {/* Posição/Medalha */}
            <div
              className="w-7 h-7 rounded-full flex items-center justify-center
                         text-xs font-bold shrink-0"
              style={{ backgroundColor: `${MEDAL_COLORS[i]}20`, color: MEDAL_COLORS[i] }}
            >
              {i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : `${i + 1}`}
            </div>

            {/* Info + Barra */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center justify-between mb-1">
                <span className="text-gray-200 text-sm font-medium truncate">{partner.paName}</span>
                <div className="flex items-center gap-1.5 shrink-0 ml-2">
                  {/* Variação % */}
                  {partner.changePercent !== 0 && (
                    <span className={`text-xs font-medium flex items-center gap-0.5 ${
                      partner.changePercent > 0 ? 'text-emerald-400' : 'text-red-400'
                    }`}>
                      {partner.changePercent > 0
                        ? <TrendingUp className="w-3 h-3" />
                        : <TrendingDown className="w-3 h-3" />}
                      {Math.abs(partner.changePercent).toFixed(1)}%
                    </span>
                  )}
                  <span className="text-white font-semibold text-sm">
                    {formatBrl(partner.totalRevenue)}
                  </span>
                </div>
              </div>

              {/* Barra de progresso animada */}
              <div className="h-1.5 bg-gray-700 rounded-full overflow-hidden">
                <div
                  className="h-full rounded-full transition-all duration-700 ease-out"
                  style={{
                    width: `${widthPct}%`,
                    backgroundColor: MEDAL_COLORS[i],
                    opacity: 0.8
                  }}
                />
              </div>

              <p className="text-gray-500 text-xs mt-1">{partner.totalOrders} pedidos · {partner.arName}</p>
            </div>
          </div>
        );
      })}
    </div>
  );
}
