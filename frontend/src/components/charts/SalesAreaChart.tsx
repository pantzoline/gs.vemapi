'use client';

import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid,
  Tooltip, ResponsiveContainer, Legend
} from 'recharts';
import type { SalesTimeSeries } from '@/types/dashboard.types';

// ─── Formatadores localizados ────────────────────────────────────────────────
const formatBrl = (value: number) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL',
    notation: value >= 1_000_000 ? 'compact' : 'standard' }).format(value);

const formatDate = (date: string) =>
  new Date(date + 'T00:00:00').toLocaleDateString('pt-BR', {
    day: '2-digit', month: 'short'
  });

// ─── Tooltip customizado ─────────────────────────────────────────────────────
function SalesTooltip({ active, payload, label }: any) {
  if (!active || !payload?.length) return null;

  return (
    <div className="bg-gray-900/95 border border-gray-700 rounded-xl p-3 shadow-2xl backdrop-blur-sm">
      <p className="text-gray-400 text-xs font-medium mb-2">
        {new Date(label + 'T00:00:00').toLocaleDateString('pt-BR', {
          weekday: 'short', day: '2-digit', month: 'long'
        })}
      </p>
      {payload.map((entry: any) => (
        <div key={entry.dataKey} className="flex items-center gap-2 text-sm">
          <span
            className="w-2 h-2 rounded-full shrink-0"
            style={{ backgroundColor: entry.color }}
          />
          <span className="text-gray-300">{entry.name}:</span>
          <span className="font-semibold text-white">
            {entry.dataKey === 'orderCount'
              ? `${entry.value} pedidos`
              : formatBrl(entry.value)}
          </span>
        </div>
      ))}
    </div>
  );
}

// ─── Estado vazio elegante ───────────────────────────────────────────────────
function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center h-64 gap-3">
      <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-blue-500/10 to-purple-500/10
                      border border-blue-500/20 flex items-center justify-center">
        <svg className="w-8 h-8 text-blue-400/60" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5}
            d="M7 12l3-3 3 3 4-4M8 21l4-4 4 4M3 4h18M4 4h16v12a1 1 0 01-1 1H5a1 1 0 01-1-1V4z"/>
        </svg>
      </div>
      <div className="text-center">
        <p className="text-gray-300 font-medium">Nenhum dado no período</p>
        <p className="text-gray-500 text-sm mt-1">
          Assim que os primeiros pedidos forem registrados, os gráficos aparecerão aqui.
        </p>
      </div>
    </div>
  );
}

// ─── Skeleton de loading ─────────────────────────────────────────────────────
export function SalesChartSkeleton() {
  return (
    <div className="animate-pulse">
      <div className="h-4 w-32 bg-gray-700 rounded mb-6" />
      <div className="h-64 bg-gradient-to-t from-gray-800/80 to-gray-700/30 rounded-lg" />
    </div>
  );
}

// ─── Componente Principal ────────────────────────────────────────────────────
type SalesChartProps = {
  data: SalesTimeSeries[];
  isLoading?: boolean;
};

export function SalesAreaChart({ data, isLoading }: SalesChartProps) {
  if (isLoading) return <SalesChartSkeleton />;
  if (!data.length) return <EmptyState />;

  return (
    <div className="w-full">
      {/* Gradientes SVG declarados inline — sem dependência de CSS global */}
      <svg width="0" height="0" className="absolute">
        <defs>
          <linearGradient id="gradRevenue" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%"  stopColor="#3b82f6" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#3b82f6" stopOpacity={0} />
          </linearGradient>
          <linearGradient id="gradNet" x1="0" y1="0" x2="0" y2="1">
            <stop offset="5%"  stopColor="#10b981" stopOpacity={0.3} />
            <stop offset="95%" stopColor="#10b981" stopOpacity={0} />
          </linearGradient>
        </defs>
      </svg>

      <ResponsiveContainer width="100%" height={280}>
        <AreaChart data={data} margin={{ top: 4, right: 4, left: 0, bottom: 0 }}>
          <defs>
            {/* Recharts precisa das defs dentro do SVG do chart também */}
            <linearGradient id="gradRevenueInner" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%"  stopColor="#3b82f6" stopOpacity={0.25} />
              <stop offset="95%" stopColor="#3b82f6" stopOpacity={0.02} />
            </linearGradient>
            <linearGradient id="gradNetInner" x1="0" y1="0" x2="0" y2="1">
              <stop offset="5%"  stopColor="#10b981" stopOpacity={0.25} />
              <stop offset="95%" stopColor="#10b981" stopOpacity={0.02} />
            </linearGradient>
          </defs>

          <CartesianGrid
            strokeDasharray="3 3"
            stroke="#374151"
            vertical={false}
          />

          <XAxis
            dataKey="date"
            tickFormatter={formatDate}
            tick={{ fill: '#9ca3af', fontSize: 11 }}
            axisLine={false}
            tickLine={false}
            interval="preserveStartEnd"
          />

          <YAxis
            tickFormatter={(v) => formatBrl(v)}
            tick={{ fill: '#9ca3af', fontSize: 11 }}
            axisLine={false}
            tickLine={false}
            width={80}
          />

          <Tooltip content={<SalesTooltip />} />

          <Legend
            iconType="circle"
            iconSize={8}
            formatter={(value) => (
              <span className="text-gray-400 text-xs">{value}</span>
            )}
          />

          <Area
            type="monotone"
            dataKey="totalRevenue"
            name="Receita Bruta"
            stroke="#3b82f6"
            strokeWidth={2}
            fill="url(#gradRevenueInner)"
            dot={false}
            activeDot={{ r: 5, fill: '#3b82f6', strokeWidth: 2, stroke: '#fff' }}
          />

          <Area
            type="monotone"
            dataKey="netRevenue"
            name="Receita Líquida"
            stroke="#10b981"
            strokeWidth={2}
            fill="url(#gradNetInner)"
            dot={false}
            activeDot={{ r: 5, fill: '#10b981', strokeWidth: 2, stroke: '#fff' }}
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}
