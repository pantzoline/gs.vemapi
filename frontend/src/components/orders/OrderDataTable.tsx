'use client';

import { useState, useMemo } from 'react';
import {
  useReactTable, getCoreRowModel, getSortedRowModel,
  type ColumnDef, type SortingState, flexRender
} from '@tanstack/react-table';
import { useOrders } from '@/hooks/useOrders';
import type { OrderSummary, OrderStatus, OrderListQuery } from '@/types/order.types';
import { STATUS_META, formatCents, formatDoc } from '@/types/order.types';
import {
  Search, Filter, ChevronUp, ChevronDown, ChevronsUpDown,
  ChevronLeft, ChevronRight, ExternalLink, Loader2
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { useRouter } from 'next/navigation';

// ─── Badge de Status ──────────────────────────────────────────────────────────
function StatusBadge({ status }: { status: OrderStatus }) {
  const meta = STATUS_META[status];
  return (
    <span className={cn(
      'inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-medium whitespace-nowrap',
      meta.color, meta.bg
    )}>
      <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: meta.dot }} />
      {meta.label}
    </span>
  );
}

// ─── Filtros de Faceta ────────────────────────────────────────────────────────
const STATUS_FILTERS: OrderStatus[] = [
  'PendingPayment', 'Paid', 'DocumentPendingValidation',
  'AwaitingVideoConference', 'IssuingAtAc', 'Issued', 'AcError', 'Cancelled'
];
const AC_PROVIDERS = ['SYNGULAR', 'VALID', 'SAFEWEB'];
const CERT_TYPES = ['A1', 'A3', 'Cloud'];

// ─── Definição das colunas ────────────────────────────────────────────────────
const COLUMNS: ColumnDef<OrderSummary>[] = [
  {
    id: 'orderNumber',
    accessorKey: 'orderNumber',
    header: 'Nº Pedido',
    size: 130,
    cell: ({ row }) => (
      <span className="font-mono text-xs text-blue-400">{row.original.orderNumber}</span>
    ),
  },
  {
    id: 'customerName',
    accessorKey: 'customerName',
    header: 'Cliente',
    size: 220,
    cell: ({ row }) => (
      <div>
        <p className="text-gray-100 font-medium text-sm truncate max-w-52">{row.original.customerName}</p>
        <p className="text-gray-500 text-xs font-mono">{formatDoc(row.original.customerDocument)}</p>
      </div>
    ),
  },
  {
    id: 'productName',
    accessorKey: 'productName',
    header: 'Produto',
    size: 160,
    cell: ({ row }) => (
      <div>
        <p className="text-gray-200 text-sm truncate max-w-40">{row.original.productName}</p>
        <p className="text-gray-500 text-xs">{row.original.acProvider} · {row.original.certificationType}</p>
      </div>
    ),
  },
  {
    id: 'status',
    accessorKey: 'status',
    header: 'Status',
    size: 160,
    cell: ({ row }) => <StatusBadge status={row.original.status} />,
  },
  {
    id: 'protocolTicket',
    accessorKey: 'protocolTicket',
    header: 'Protocolo',
    size: 140,
    cell: ({ row }) => row.original.protocolTicket
      ? <span className="font-mono text-xs text-gray-300">{row.original.protocolTicket}</span>
      : <span className="text-gray-600 text-xs">—</span>,
    enableSorting: false,
  },
  {
    id: 'totalAmount',
    accessorKey: 'totalAmountInCents',
    header: 'Valor',
    size: 110,
    cell: ({ row }) => (
      <span className="text-gray-200 font-semibold text-sm">
        {formatCents(row.original.totalAmountInCents)}
      </span>
    ),
  },
  {
    id: 'paName',
    accessorKey: 'paName',
    header: 'Parceiro (PA)',
    size: 150,
    cell: ({ row }) => (
      <span className="text-gray-400 text-xs truncate max-w-36">{row.original.paName}</span>
    ),
  },
  {
    id: 'createdAt',
    accessorKey: 'createdAt',
    header: 'Data',
    size: 110,
    cell: ({ row }) => (
      <span className="text-gray-400 text-xs">
        {new Date(row.original.createdAt).toLocaleDateString('pt-BR')}
      </span>
    ),
  },
];

// ─── Componente Principal ─────────────────────────────────────────────────────
export function OrderDataTable() {
  const router = useRouter();

  // ─── Estado da tabela ────────────────────────────────────────────────────
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [sorting, setSorting] = useState<SortingState>([]);
  const [selectedStatuses, setSelectedStatuses] = useState<OrderStatus[]>([]);
  const [selectedAcs, setSelectedAcs] = useState<string[]>([]);
  const [selectedCerts, setSelectedCerts] = useState<string[]>([]);
  const [showFilters, setShowFilters] = useState(false);

  // Debounce da busca para não disparar uma request por tecla
  const searchDebounceRef = useState<ReturnType<typeof setTimeout>>();
  const handleSearch = (val: string) => {
    setSearch(val);
    clearTimeout(searchDebounceRef[0]);
    (searchDebounceRef as any)[0] = setTimeout(() => {
      setDebouncedSearch(val);
      setPage(1);
    }, 400);
  };

  // ─── Query ───────────────────────────────────────────────────────────────
  const query: OrderListQuery = {
    page,
    pageSize: 15,
    search: debouncedSearch || undefined,
    sortBy: sorting[0]?.id,
    sortDesc: sorting[0]?.desc ?? true,
    statuses: selectedStatuses.length ? selectedStatuses : undefined,
    acProviders: selectedAcs.length ? selectedAcs : undefined,
    certTypes: selectedCerts.length ? (selectedCerts as any) : undefined,
  };

  const { data, isLoading, isFetching } = useOrders(query);

  // ─── Tabela ───────────────────────────────────────────────────────────────
  const table = useReactTable({
    data: data?.items ?? [],
    columns: COLUMNS,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    manualSorting: true, // Sorting é server-side
    manualPagination: true,
    state: { sorting },
    onSortingChange: (updater) => { setSorting(updater); setPage(1); },
    pageCount: data?.totalPages ?? 0,
  });

  // ─── Toggle helpers ───────────────────────────────────────────────────────
  const toggleFilter = <T,>(list: T[], setList: (v: T[]) => void, value: T) => {
    setList(list.includes(value) ? list.filter(v => v !== value) : [...list, value]);
    setPage(1);
  };

  const activeFilterCount = selectedStatuses.length + selectedAcs.length + selectedCerts.length;

  return (
    <div className="flex flex-col gap-4">
      {/* ─── Toolbar ─────────────────────────────────────────────────────── */}
      <div className="flex flex-col sm:flex-row gap-3">
        {/* Busca global */}
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-500" />
          <input
            type="text"
            value={search}
            onChange={e => handleSearch(e.target.value)}
            placeholder="Buscar por nome, CPF ou protocolo..."
            className="w-full pl-9 pr-4 py-2.5 bg-gray-800/60 border border-gray-700/60
                       text-gray-200 placeholder-gray-500 text-sm rounded-xl
                       focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30
                       transition-all"
          />
          {isFetching && (
            <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4
                                text-gray-500 animate-spin" />
          )}
        </div>

        {/* Toggle de Filtros */}
        <button
          onClick={() => setShowFilters(v => !v)}
          className={cn(
            'flex items-center gap-2 px-4 py-2.5 rounded-xl border text-sm font-medium transition-all',
            showFilters || activeFilterCount > 0
              ? 'bg-blue-600/20 border-blue-500/50 text-blue-400'
              : 'bg-gray-800/60 border-gray-700/60 text-gray-400 hover:text-gray-200'
          )}
        >
          <Filter className="w-4 h-4" />
          Filtros
          {activeFilterCount > 0 && (
            <span className="px-1.5 py-0.5 rounded-full bg-blue-600 text-white text-xs">
              {activeFilterCount}
            </span>
          )}
        </button>
      </div>

      {/* ─── Painel de filtros por faceta ─────────────────────────────────── */}
      {showFilters && (
        <div className="p-4 bg-gray-800/40 border border-gray-700/40 rounded-xl
                        animate-in fade-in slide-in-from-top-2 duration-200 space-y-3">
          {/* Status */}
          <div>
            <p className="text-gray-500 text-xs font-semibold uppercase tracking-wider mb-2">Status</p>
            <div className="flex flex-wrap gap-1.5">
              {STATUS_FILTERS.map(s => {
                const meta = STATUS_META[s];
                const active = selectedStatuses.includes(s);
                return (
                  <button key={s}
                    onClick={() => toggleFilter(selectedStatuses, setSelectedStatuses, s)}
                    className={cn(
                      'flex items-center gap-1.5 px-2.5 py-1 rounded-lg text-xs font-medium transition-all',
                      active
                        ? cn(meta.color, meta.bg, 'ring-1 ring-current')
                        : 'text-gray-500 bg-gray-700/40 hover:bg-gray-700 hover:text-gray-300'
                    )}
                  >
                    <span className="w-1.5 h-1.5 rounded-full" style={{ backgroundColor: active ? meta.dot : '#4b5563' }} />
                    {meta.label}
                  </button>
                );
              })}
            </div>
          </div>

          {/* AC e Tipo */}
          <div className="flex flex-wrap gap-6">
            <div>
              <p className="text-gray-500 text-xs font-semibold uppercase tracking-wider mb-2">Autoridade Certificadora</p>
              <div className="flex gap-1.5">
                {AC_PROVIDERS.map(ac => (
                  <button key={ac}
                    onClick={() => toggleFilter(selectedAcs, setSelectedAcs, ac)}
                    className={cn(
                      'px-3 py-1 rounded-lg text-xs font-medium transition-all',
                      selectedAcs.includes(ac)
                        ? 'bg-indigo-600/30 text-indigo-300 ring-1 ring-indigo-500'
                        : 'bg-gray-700/40 text-gray-500 hover:text-gray-300 hover:bg-gray-700'
                    )}
                  >
                    {ac}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <p className="text-gray-500 text-xs font-semibold uppercase tracking-wider mb-2">Tipo</p>
              <div className="flex gap-1.5">
                {CERT_TYPES.map(ct => (
                  <button key={ct}
                    onClick={() => toggleFilter(selectedCerts, setSelectedCerts, ct)}
                    className={cn(
                      'px-3 py-1 rounded-lg text-xs font-medium transition-all',
                      selectedCerts.includes(ct)
                        ? 'bg-violet-600/30 text-violet-300 ring-1 ring-violet-500'
                        : 'bg-gray-700/40 text-gray-500 hover:text-gray-300 hover:bg-gray-700'
                    )}
                  >
                    {ct}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {activeFilterCount > 0 && (
            <button
              onClick={() => { setSelectedStatuses([]); setSelectedAcs([]); setSelectedCerts([]); }}
              className="text-xs text-gray-500 hover:text-red-400 transition-colors"
            >
              ✕ Limpar todos os filtros
            </button>
          )}
        </div>
      )}

      {/* ─── Tabela ───────────────────────────────────────────────────────── */}
      <div className="bg-gray-800/50 border border-gray-700/40 rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-max">
            {/* Header */}
            <thead>
              {table.getHeaderGroups().map(hg => (
                <tr key={hg.id} className="border-b border-gray-700/60">
                  {hg.headers.map(header => (
                    <th key={header.id}
                      className={cn(
                        'px-4 py-3 text-left text-xs font-semibold text-gray-500',
                        'uppercase tracking-wider whitespace-nowrap',
                        header.column.getCanSort() && 'cursor-pointer select-none hover:text-gray-300 transition-colors'
                      )}
                      style={{ width: header.getSize() }}
                      onClick={header.column.getToggleSortingHandler()}
                    >
                      <div className="flex items-center gap-1.5">
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {header.column.getCanSort() && (
                          <span className="text-gray-600">
                            {{ asc: <ChevronUp className="w-3 h-3 text-blue-400" />,
                               desc: <ChevronDown className="w-3 h-3 text-blue-400" />
                             }[header.column.getIsSorted() as string]
                              ?? <ChevronsUpDown className="w-3 h-3" />}
                          </span>
                        )}
                      </div>
                    </th>
                  ))}
                  <th className="px-4 py-3 w-10" />
                </tr>
              ))}
            </thead>

            {/* Body */}
            <tbody>
              {isLoading ? (
                [...Array(8)].map((_, i) => (
                  <tr key={i} className="border-b border-gray-700/30">
                    {COLUMNS.map((_, ci) => (
                      <td key={ci} className="px-4 py-3.5">
                        <div className="h-4 bg-gray-700/60 rounded animate-pulse"
                          style={{ width: `${60 + (ci * 7) % 30}%` }}
                        />
                      </td>
                    ))}
                    <td />
                  </tr>
                ))
              ) : table.getRowModel().rows.length === 0 ? (
                <tr>
                  <td colSpan={COLUMNS.length + 1} className="py-16 text-center">
                    <div className="flex flex-col items-center gap-2">
                      <Search className="w-8 h-8 text-gray-600" />
                      <p className="text-gray-400 font-medium">Nenhum pedido encontrado</p>
                      <p className="text-gray-600 text-sm">Tente ajustar os filtros ou a busca.</p>
                    </div>
                  </td>
                </tr>
              ) : (
                table.getRowModel().rows.map(row => (
                  <tr key={row.id}
                    className="border-b border-gray-700/20 hover:bg-white/[0.02]
                               transition-colors cursor-pointer group"
                    onClick={() => router.push(`/dashboard/orders/${row.original.id}`)}
                  >
                    {row.getVisibleCells().map(cell => (
                      <td key={cell.id} className="px-4 py-3.5">
                        {flexRender(cell.column.columnDef.cell, cell.getContext())}
                      </td>
                    ))}
                    <td className="px-2 py-3.5">
                      <ExternalLink className="w-4 h-4 text-gray-600 group-hover:text-blue-400
                                              transition-colors" />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* ─── Paginação ─────────────────────────────────────────────────── */}
        <div className="flex items-center justify-between px-4 py-3 border-t border-gray-700/40">
          <p className="text-gray-500 text-xs">
            {data ? (
              <>
                <span className="text-gray-300 font-medium">{data.totalCount}</span> pedidos ·{' '}
                página <span className="text-gray-300 font-medium">{page}</span> de{' '}
                <span className="text-gray-300 font-medium">{data.totalPages}</span>
              </>
            ) : '—'}
          </p>

          <div className="flex items-center gap-1">
            <button
              onClick={() => setPage(p => Math.max(1, p - 1))}
              disabled={!data?.hasPrevPage}
              className="p-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-700
                         disabled:opacity-30 disabled:cursor-not-allowed transition-all"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>

            {/* Páginas numeradas (max 5 botões) */}
            {data && Array.from({ length: Math.min(5, data.totalPages) }, (_, i) => {
              const p = Math.max(1, Math.min(page - 2, data.totalPages - 4)) + i;
              return (
                <button key={p} onClick={() => setPage(p)}
                  className={cn(
                    'w-8 h-8 rounded-lg text-xs font-medium transition-all',
                    p === page
                      ? 'bg-blue-600 text-white'
                      : 'text-gray-400 hover:bg-gray-700 hover:text-white'
                  )}
                >
                  {p}
                </button>
              );
            })}

            <button
              onClick={() => setPage(p => p + 1)}
              disabled={!data?.hasNextPage}
              className="p-1.5 rounded-lg text-gray-400 hover:text-white hover:bg-gray-700
                         disabled:opacity-30 disabled:cursor-not-allowed transition-all"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
