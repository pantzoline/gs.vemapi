'use client';

import { useState } from 'react';
import { useTenantHierarchy } from '@/hooks/useTenantSettings';
import type { TenantHierarchy } from '@/types/management.types';
import { TENANT_LEVEL_META } from '@/types/management.types';
import {
  ChevronRight, ChevronDown, Users, FileText,
  Plus, Settings, Power, Loader2, Building2
} from 'lucide-react';
import { cn } from '@/lib/utils';

// ─── Nó da Árvore ─────────────────────────────────────────────────────────────
function TenantNode({
  tenant,
  depth = 0,
  onSelect,
  selectedId,
}: {
  tenant: TenantHierarchy;
  depth?: number;
  onSelect: (t: TenantHierarchy) => void;
  selectedId?: string;
}) {
  const [isExpanded, setIsExpanded] = useState(depth < 1);
  const meta = TENANT_LEVEL_META[tenant.level as keyof typeof TENANT_LEVEL_META];
  const hasChildren = tenant.children.length > 0;
  const isSelected = tenant.id === selectedId;

  const indentPx = depth * 20;

  return (
    <div>
      {/* Linha do nó */}
      <div
        className={cn(
          'group flex items-center gap-2 px-3 py-2.5 rounded-xl cursor-pointer',
          'transition-all duration-150 select-none',
          isSelected
            ? 'bg-blue-600/20 border border-blue-500/40'
            : 'hover:bg-gray-700/30 border border-transparent'
        )}
        style={{ marginLeft: `${indentPx}px` }}
        onClick={() => onSelect(tenant)}
      >
        {/* Expand/Collapse */}
        <button
          onClick={(e) => { e.stopPropagation(); setIsExpanded(v => !v); }}
          className={cn(
            'w-4 h-4 shrink-0 text-gray-500 transition-transform duration-200',
            !hasChildren && 'invisible',
            isExpanded && 'rotate-90'
          )}
        >
          <ChevronRight className="w-full h-full" />
        </button>

        {/* Ícone do nível */}
        <span className="text-base leading-none">{meta.icon}</span>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className={cn(
              'font-medium text-sm truncate',
              isSelected ? 'text-white' : 'text-gray-200'
            )}>
              {tenant.name}
            </span>
            {!tenant.isActive && (
              <span className="px-1.5 py-0.5 text-[10px] font-medium rounded-full
                               bg-red-900/40 text-red-400 border border-red-700/40 shrink-0">
                Inativo
              </span>
            )}
          </div>
          <div className="flex items-center gap-2 mt-0.5">
            <span className={cn('text-[10px] font-semibold px-1.5 py-0.5 rounded-full',
              meta.color, meta.bg)}>
              {meta.shortLabel}
            </span>
            {hasChildren && (
              <span className="text-gray-600 text-xs">
                {tenant.children.length} sub-unidade{tenant.children.length !== 1 ? 's' : ''}
              </span>
            )}
            {tenant.contractCount > 0 && (
              <span className="text-gray-600 text-xs">
                · {tenant.contractCount} contrato{tenant.contractCount !== 1 ? 's' : ''}
              </span>
            )}
          </div>
        </div>

        {/* Cor indicadora (dot de white label) */}
        <div
          className="w-2.5 h-2.5 rounded-full shrink-0 ring-1 ring-white/20 opacity-60
                     group-hover:opacity-100 transition-opacity"
          style={{ backgroundColor: tenant.primaryColor }}
          title="Cor do parceiro"
        />
      </div>

      {/* Filhos (animação collapse) */}
      {isExpanded && hasChildren && (
        <div className="mt-0.5 space-y-0.5 animate-in fade-in duration-200">
          {tenant.children.map(child => (
            <TenantNode
              key={child.id}
              tenant={child}
              depth={depth + 1}
              onSelect={onSelect}
              selectedId={selectedId}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ─── Painel de Detalhes do Tenant Selecionado ─────────────────────────────────
function TenantDetailPanel({
  tenant,
  onCreateChild,
}: {
  tenant: TenantHierarchy;
  onCreateChild?: (parent: TenantHierarchy) => void;
}) {
  const meta = TENANT_LEVEL_META[tenant.level as keyof typeof TENANT_LEVEL_META];

  return (
    <div className="h-full flex flex-col gap-4 p-4">
      {/* Header */}
      <div className="flex items-start gap-3">
        <div
          className="w-12 h-12 rounded-2xl flex items-center justify-center text-2xl shrink-0"
          style={{ backgroundColor: `${tenant.primaryColor}20`,
                   border: `1px solid ${tenant.primaryColor}40` }}
        >
          {meta.icon}
        </div>
        <div className="flex-1 min-w-0">
          <h2 className="text-white font-bold text-lg leading-tight truncate">{tenant.name}</h2>
          <div className="flex items-center gap-2 mt-1">
            <span className={cn('text-xs font-semibold px-2 py-0.5 rounded-full', meta.color, meta.bg)}>
              {meta.label}
            </span>
            <span className={cn(
              'text-xs px-2 py-0.5 rounded-full font-medium',
              tenant.isActive
                ? 'text-emerald-400 bg-emerald-400/10'
                : 'text-red-400 bg-red-400/10'
            )}>
              {tenant.isActive ? 'Ativo' : 'Inativo'}
            </span>
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-3">
        <div className="p-3 bg-gray-700/30 rounded-xl">
          <div className="flex items-center gap-1.5 mb-1">
            <Users className="w-3.5 h-3.5 text-gray-400" />
            <span className="text-gray-400 text-xs">Sub-unidades</span>
          </div>
          <p className="text-white font-bold text-xl">{tenant.children.length}</p>
        </div>
        <div className="p-3 bg-gray-700/30 rounded-xl">
          <div className="flex items-center gap-1.5 mb-1">
            <FileText className="w-3.5 h-3.5 text-gray-400" />
            <span className="text-gray-400 text-xs">Contratos</span>
          </div>
          <p className="text-white font-bold text-xl">{tenant.contractCount}</p>
        </div>
      </div>

      {/* Prévia de White Label */}
      <div className="p-3 bg-gray-700/20 border border-gray-700/40 rounded-xl">
        <p className="text-gray-500 text-xs font-semibold uppercase tracking-wider mb-2">White Label</p>
        <div className="flex items-center gap-3">
          <div className="flex gap-2">
            {[tenant.primaryColor].map((color, i) => (
              <div key={i} className="w-6 h-6 rounded-full ring-1 ring-white/20"
                style={{ backgroundColor: color }} />
            ))}
          </div>
          <span className="text-gray-400 text-xs font-mono">{tenant.primaryColor}</span>
        </div>
      </div>

      {/* Ações */}
      <div className="mt-auto space-y-2">
        {tenant.level !== 'PointOfAttendance' && (
          <button
            onClick={() => onCreateChild?.(tenant)}
            className="w-full flex items-center justify-center gap-2
                       px-4 py-2.5 bg-blue-600 hover:bg-blue-500
                       text-white text-sm font-medium rounded-xl transition-all"
          >
            <Plus className="w-4 h-4" />
            Adicionar {tenant.level === 'Master' ? 'AR' : 'PA'}
          </button>
        )}
        <button className="w-full flex items-center justify-center gap-2
                           px-4 py-2.5 bg-gray-700/40 hover:bg-gray-700
                           text-gray-300 text-sm font-medium rounded-xl transition-all">
          <Settings className="w-4 h-4" />
          Configurações
        </button>
      </div>
    </div>
  );
}

// ─── Componente Principal ─────────────────────────────────────────────────────
type PartnerHierarchyProps = {
  onCreateAr?: () => void;
  onCreatePa?: (parentArId: string) => void;
};

export function PartnerHierarchy({ onCreateAr, onCreatePa }: PartnerHierarchyProps) {
  const { data: hierarchy, isLoading } = useTenantHierarchy();
  const [selectedTenant, setSelectedTenant] =
    useState<TenantHierarchy | null>(null);

  const handleCreateChild = (parent: TenantHierarchy) => {
    if (parent.level === 'Master') onCreateAr?.();
    else if (parent.level === 'AuthorityRegistrar') onCreatePa?.(parent.id);
  };

  if (isLoading) {
    return (
      <div className="flex gap-4 h-96">
        <div className="flex-1 space-y-2">
          {[...Array(5)].map((_, i) => (
            <div key={i} className="h-14 bg-gray-800/60 rounded-xl animate-pulse"
              style={{ marginLeft: `${(i % 3) * 20}px` }} />
          ))}
        </div>
        <div className="w-64 bg-gray-800/60 rounded-2xl animate-pulse" />
      </div>
    );
  }

  if (!hierarchy) {
    return (
      <div className="flex flex-col items-center justify-center h-48 gap-2">
        <Building2 className="w-8 h-8 text-gray-600" />
        <p className="text-gray-500 text-sm">Nenhuma rede configurada.</p>
      </div>
    );
  }

  return (
    <div className="flex gap-4 min-h-96">
      {/* Coluna da Árvore */}
      <div className="flex-1 bg-gray-800/40 border border-gray-700/40
                      rounded-2xl p-3 overflow-y-auto">
        <TenantNode
          tenant={hierarchy}
          depth={0}
          onSelect={setSelectedTenant}
          selectedId={selectedTenant?.id}
        />
      </div>

      {/* Painel de Detalhes */}
      <div className={cn(
        'w-72 bg-gray-800/40 border border-gray-700/40 rounded-2xl',
        'transition-all duration-300',
        !selectedTenant && 'opacity-0 pointer-events-none'
      )}>
        {selectedTenant && (
          <TenantDetailPanel
            tenant={selectedTenant}
            onCreateChild={handleCreateChild}
          />
        )}
      </div>
    </div>
  );
}
