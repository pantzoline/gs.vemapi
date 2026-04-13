'use client';

import { PieChart, Pie, Cell, Tooltip, Legend, ResponsiveContainer } from 'recharts';
import type { OrderStatusDistribution } from '@/types/dashboard.types';

function StatusTooltip({ active, payload }: any) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload as OrderStatusDistribution;

  return (
    <div className="bg-gray-900/95 border border-gray-700 rounded-xl p-3 shadow-2xl backdrop-blur-sm min-w-40">
      <div className="flex items-center gap-2 mb-1">
        <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: d.color }} />
        <span className="text-white font-medium text-sm">{d.status}</span>
      </div>
      <p className="text-gray-400 text-xs">
        {d.count} pedido{d.count !== 1 ? 's' : ''} ·{' '}
        <span className="text-white font-semibold">{d.percentage}%</span>
      </p>
    </div>
  );
}

// Legenda customizada com contadores
function CustomLegend({ data }: { data: OrderStatusDistribution[] }) {
  return (
    <div className="grid grid-cols-2 gap-x-4 gap-y-2 mt-4">
      {data.map((item) => (
        <div key={item.statusKey} className="flex items-center gap-2">
          <span className="w-2 h-2 rounded-full shrink-0" style={{ backgroundColor: item.color }} />
          <span className="text-gray-400 text-xs truncate">{item.status}</span>
          <span className="text-gray-200 text-xs font-semibold ml-auto">{item.count}</span>
        </div>
      ))}
    </div>
  );
}

function EmptyDonut() {
  return (
    <div className="flex flex-col items-center justify-center h-52 gap-2">
      <div className="w-24 h-24 rounded-full border-4 border-gray-700 border-dashed
                      flex items-center justify-center">
        <span className="text-gray-600 text-2xl font-bold">0</span>
      </div>
      <p className="text-gray-500 text-sm">Nenhum pedido no período</p>
    </div>
  );
}

export function OrderStatusPieChart({
  data,
  isLoading
}: {
  data: OrderStatusDistribution[];
  isLoading?: boolean;
}) {
  if (isLoading) {
    return (
      <div className="animate-pulse flex flex-col items-center gap-4">
        <div className="w-40 h-40 rounded-full bg-gray-700/50" />
        <div className="w-full grid grid-cols-2 gap-2">
          {[...Array(4)].map((_, i) => (
            <div key={i} className="h-3 bg-gray-700 rounded" />
          ))}
        </div>
      </div>
    );
  }

  if (!data.length) return <EmptyDonut />;

  const total = data.reduce((acc, d) => acc + d.count, 0);

  return (
    <div>
      {/* Label central com total */}
      <div className="relative">
        <ResponsiveContainer width="100%" height={200}>
          <PieChart>
            <Pie
              data={data}
              dataKey="count"
              nameKey="status"
              cx="50%"
              cy="50%"
              innerRadius={55}
              outerRadius={85}
              paddingAngle={2}
              strokeWidth={0}
            >
              {data.map((entry) => (
                <Cell key={entry.statusKey} fill={entry.color} />
              ))}
            </Pie>
            <Tooltip content={<StatusTooltip />} />
          </PieChart>
        </ResponsiveContainer>

        {/* Total no centro do donut */}
        <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
          <span className="text-2xl font-bold text-white">{total}</span>
          <span className="text-gray-500 text-xs">pedidos</span>
        </div>
      </div>

      <CustomLegend data={data} />
    </div>
  );
}
