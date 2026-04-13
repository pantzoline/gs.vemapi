'use client';

import { useState, useCallback } from 'react';
import type { DashboardFilter, PeriodPreset } from '@/types/dashboard.types';
import { PERIOD_PRESETS, buildDateRange } from '@/types/dashboard.types';
import { Calendar, ChevronDown } from 'lucide-react';

type PeriodFilterProps = {
  value: DashboardFilter;
  onChange: (filter: DashboardFilter) => void;
};

export function PeriodFilter({ value, onChange }: PeriodFilterProps) {
  const [showCustom, setShowCustom] = useState(false);
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');

  const handlePreset = useCallback((preset: PeriodPreset) => {
    if (preset === 'custom') {
      setShowCustom(true);
      return;
    }
    setShowCustom(false);
    const { from, to } = buildDateRange(preset);
    onChange({ preset, from, to });
  }, [onChange]);

  const handleCustomApply = useCallback(() => {
    if (!customFrom || !customTo) return;
    const from = new Date(customFrom + 'T00:00:00');
    const to   = new Date(customTo   + 'T23:59:59');
    if (from > to) return;
    onChange({ preset: 'custom', from, to });
    setShowCustom(false);
  }, [customFrom, customTo, onChange]);

  return (
    <div className="flex flex-col sm:flex-row items-start sm:items-center gap-2">
      {/* Botões de preset */}
      <div className="flex items-center gap-1 bg-gray-800/60 border border-gray-700/60
                      rounded-xl p-1 backdrop-blur-sm">
        {(Object.entries(PERIOD_PRESETS) as [PeriodPreset, { label: string }][]).map(
          ([preset, { label }]) => (
            <button
              key={preset}
              onClick={() => handlePreset(preset)}
              className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-all duration-200
                ${value.preset === preset
                  ? 'bg-blue-600 text-white shadow-lg shadow-blue-600/30'
                  : 'text-gray-400 hover:text-gray-200 hover:bg-gray-700/60'
                }`}
            >
              {label}
            </button>
          )
        )}

        <button
          onClick={() => handlePreset('custom')}
          className={`flex items-center gap-1.5 px-3 py-1.5 text-xs font-medium rounded-lg
                      transition-all duration-200
            ${value.preset === 'custom'
              ? 'bg-blue-600 text-white shadow-lg shadow-blue-600/30'
              : 'text-gray-400 hover:text-gray-200 hover:bg-gray-700/60'
            }`}
        >
          <Calendar className="w-3 h-3" />
          Personalizado
        </button>
      </div>

      {/* Range selecionado */}
      <span className="text-gray-500 text-xs hidden sm:block">
        {value.from.toLocaleDateString('pt-BR')} → {value.to.toLocaleDateString('pt-BR')}
      </span>

      {/* Picker customizado */}
      {showCustom && (
        <div className="flex items-center gap-2 p-3 bg-gray-800 border border-gray-700
                        rounded-xl shadow-xl animate-in fade-in slide-in-from-top-2 duration-200">
          <input
            type="date"
            value={customFrom}
            onChange={e => setCustomFrom(e.target.value)}
            max={customTo || undefined}
            className="bg-gray-700 border border-gray-600 text-gray-200 text-xs
                       rounded-lg px-2 py-1.5 focus:outline-none focus:border-blue-500"
          />
          <span className="text-gray-500 text-xs">até</span>
          <input
            type="date"
            value={customTo}
            onChange={e => setCustomTo(e.target.value)}
            min={customFrom || undefined}
            max={new Date().toISOString().split('T')[0]}
            className="bg-gray-700 border border-gray-600 text-gray-200 text-xs
                       rounded-lg px-2 py-1.5 focus:outline-none focus:border-blue-500"
          />
          <button
            onClick={handleCustomApply}
            disabled={!customFrom || !customTo}
            className="px-3 py-1.5 bg-blue-600 text-white text-xs font-medium rounded-lg
                       hover:bg-blue-500 disabled:opacity-40 disabled:cursor-not-allowed
                       transition-colors"
          >
            Aplicar
          </button>
          <button
            onClick={() => setShowCustom(false)}
            className="text-gray-500 hover:text-gray-300 text-xs transition-colors"
          >
            ✕
          </button>
        </div>
      )}
    </div>
  );
}
