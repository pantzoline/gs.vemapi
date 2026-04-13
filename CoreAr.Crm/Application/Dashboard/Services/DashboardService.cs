using CoreAr.Crm.Application.Dashboard.DTOs;
using CoreAr.Identity.Application.Services;
using CoreAr.Identity.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace CoreAr.Crm.Application.Dashboard.Services;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(DashboardFilterQuery filter, CancellationToken ct = default);
    Task<DashboardChartsDto> GetChartsAsync(DashboardFilterQuery filter, CancellationToken ct = default);
}

/// <summary>
/// Serviço de agregação para o Dashboard de BI.
///
/// REGRA DE OURO: AsNoTracking() em TODAS as queries aqui.
/// Este serviço é READ-ONLY — nenhuma entidade deve ser rastreada pelo EF.
///
/// O ICurrentUserService injeta o TenantId do JWT, usado pelo Global Query Filter
/// do CrmDbContext para isolamento automático por tenant.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUserService _currentUser;

    // Mapa de cores fixas por status (frontend usa para consistência visual)
    private static readonly Dictionary<string, string> StatusColors = new()
    {
        ["PENDING_PAYMENT"] = "#f59e0b",
        ["PAID"]            = "#3b82f6",
        ["VALIDATED"]       = "#8b5cf6",
        ["ISSUED"]          = "#10b981",
        ["CANCELLED"]       = "#ef4444",
        ["EXPIRED"]         = "#6b7280",
    };

    public DashboardService(CrmDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    // ─── /api/dashboard/summary ───────────────────────────────────────────────
    public async Task<DashboardSummaryDto> GetSummaryAsync(
        DashboardFilterQuery filter, CancellationToken ct = default)
    {
        // O Global Query Filter já aplicou o WHERE por TenantId automaticamente
        var baseQuery = _db.Orders
            .AsNoTracking()                                // READ-ONLY — sem overhead de tracking
            .Where(o => o.CreatedAt >= filter.From && o.CreatedAt <= filter.To);

        // Período anterior de mesmo tamanho (para calcular a variação %)
        var duration = filter.To - filter.From;
        var prevFrom = filter.From - duration;
        var prevQuery = _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= prevFrom && o.CreatedAt < filter.From);

        // ─── Executa as agregações em paralelo (minimiza round-trips ao DB) ──
        var (currentStats, prevStats) = await (
            baseQuery.GroupBy(_ => 1).Select(g => new
            {
                TotalRevenue   = g.Sum(o => o.TotalAmountInCents),
                NetRevenue     = g.Sum(o => o.NetAmountInCents),      // Após split
                OrderCount     = g.Count(),
                IssuedCount    = g.Count(o => o.Status == OrderStatus.Issued),
            }).FirstOrDefaultAsync(ct),

            prevQuery.GroupBy(_ => 1).Select(g => new
            {
                TotalRevenue   = g.Sum(o => o.TotalAmountInCents),
                NetRevenue     = g.Sum(o => o.NetAmountInCents),
                OrderCount     = g.Count(),
                IssuedCount    = g.Count(o => o.Status == OrderStatus.Issued),
            }).FirstOrDefaultAsync(ct)
        ).WhenBoth(); // Extension para Task.WhenAll com desestruturação

        // Valores em centavos → Reais (divisão segura com long)
        var totalRevenueReais = (currentStats?.TotalRevenue ?? 0) / 100m;
        var netRevenueReais   = (currentStats?.NetRevenue ?? 0) / 100m;
        var orderCount        = currentStats?.OrderCount ?? 0;
        var conversionRate    = orderCount > 0
            ? (currentStats!.IssuedCount / (decimal)orderCount) * 100
            : 0m;

        var avgTicket = orderCount > 0 ? totalRevenueReais / orderCount : 0m;

        // ─── Cálculo de variação vs. período anterior ─────────────────────────
        var prevRevenueReais = (prevStats?.TotalRevenue ?? 0) / 100m;
        var prevNetReais     = (prevStats?.NetRevenue ?? 0) / 100m;
        var prevOrderCount   = prevStats?.OrderCount ?? 0;
        var prevConversion   = prevOrderCount > 0
            ? (prevStats!.IssuedCount / (decimal)prevOrderCount) * 100
            : 0m;

        return new DashboardSummaryDto
        {
            TenantLevel = _currentUser.TenantLevel.ToString(),
            Period      = new DateRangeDto(filter.From, filter.To),

            TotalSales = BuildKpi(
                "Total de Vendas", totalRevenueReais, prevRevenueReais,
                FormatBrl(totalRevenueReais), "R$"),

            NetRevenue = BuildKpi(
                "Receita Líquida", netRevenueReais, prevNetReais,
                FormatBrl(netRevenueReais), "R$"),

            AverageTicket = BuildKpi(
                "Ticket Médio", avgTicket,
                prevOrderCount > 0 ? prevNetReais / prevOrderCount : 0m,
                FormatBrl(avgTicket), "R$"),

            ProtocolConversion = BuildKpi(
                "Conversão de Protocolos", conversionRate, prevConversion,
                $"{conversionRate:F1}%", "%"),
        };
    }

    // ─── /api/dashboard/charts ────────────────────────────────────────────────
    public async Task<DashboardChartsDto> GetChartsAsync(
        DashboardFilterQuery filter, CancellationToken ct = default)
    {
        var baseQuery = _db.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAt >= filter.From && o.CreatedAt <= filter.To);

        // ─── Série Temporal (AreaChart) ───────────────────────────────────────
        // Agrupa por dia e projeta APENAS os campos necessários (não traz entidade inteira)
        var timeSeries = await baseQuery
            .GroupBy(o => o.CreatedAt.Date)
            .Select(g => new SalesTimeSeriesDto
            {
                Date         = g.Key.ToString("yyyy-MM-dd"),
                TotalRevenue = g.Sum(o => o.TotalAmountInCents) / 100m,
                NetRevenue   = g.Sum(o => o.NetAmountInCents) / 100m,
                OrderCount   = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToListAsync(ct);

        // ─── Distribuição por Status (PieChart) ───────────────────────────────
        var totalOrders = await baseQuery.CountAsync(ct);
        var statusDist = await baseQuery
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var statusDistribution = statusDist.Select(s =>
        {
            var statusKey = s.Status.ToString().ToUpperInvariant();
            return new OrderStatusDistributionDto
            {
                Status     = TranslateStatus(s.Status),
                StatusKey  = statusKey,
                Count      = s.Count,
                Percentage = totalOrders > 0 ? Math.Round((s.Count / (decimal)totalOrders) * 100, 1) : 0,
                Color      = StatusColors.GetValueOrDefault(statusKey, "#94a3b8")
            };
        }).OrderByDescending(x => x.Count).ToList();

        // ─── Ranking de Parceiros (apenas Master/AdminAr) ─────────────────────
        IReadOnlyList<PartnerRankingDto>? partnerRanking = null;
        if (_currentUser.IsMaster || _currentUser.IsAdminAr)
        {
            partnerRanking = await _db.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= filter.From && o.CreatedAt <= filter.To)
                // ATENÇÃO: sem Global Query Filter aqui para AR — vê TODOS os seus PAs
                .GroupBy(o => new { o.PaId, o.PaName, o.ArName })
                .Select(g => new
                {
                    g.Key.PaId, g.Key.PaName, g.Key.ArName,
                    TotalOrders  = g.Count(),
                    TotalRevenue = g.Sum(o => o.TotalAmountInCents) / 100m,
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(5)
                .Select((x, i) => new PartnerRankingDto
                {
                    Rank         = i + 1,
                    PaId         = x.PaId,
                    PaName       = x.PaName,
                    ArName       = x.ArName,
                    TotalOrders  = x.TotalOrders,
                    TotalRevenue = x.TotalRevenue,
                    ChangePercent = 0m // Calculado em query separada se necessário
                })
                .ToListAsync(ct);
        }

        // ─── Receita por Produto (BarChart horizontal) ────────────────────────
        var revenueByProduct = await baseQuery
            .GroupBy(o => new { o.ProductCode, o.ProductName })
            .Select(g => new RevenueByProductDto
            {
                ProductCode = g.Key.ProductCode,
                ProductName = g.Key.ProductName,
                Revenue     = g.Sum(o => o.TotalAmountInCents) / 100m,
                Quantity    = g.Count()
            })
            .OrderByDescending(x => x.Revenue)
            .Take(8)
            .ToListAsync(ct);

        return new DashboardChartsDto
        {
            SalesTimeSeries  = timeSeries,
            StatusDistribution = statusDistribution,
            PartnerRanking   = partnerRanking,
            RevenueByProduct = revenueByProduct,
        };
    }

    // ─── Helpers privados ─────────────────────────────────────────────────────
    private static KpiCardDto BuildKpi(
        string label, decimal current, decimal previous,
        string formatted, string unit)
    {
        var change = previous > 0
            ? Math.Round(((current - previous) / previous) * 100, 1)
            : (decimal?)null;

        return new KpiCardDto
        {
            Label          = label,
            Value          = current,
            FormattedValue = formatted,
            ChangePercent  = change,
            Unit           = unit,
            Trend          = change switch
            {
                null         => TrendDirection.Neutral,
                > 0          => TrendDirection.Up,
                < 0          => TrendDirection.Down,
                _            => TrendDirection.Neutral,
            }
        };
    }

    private static string FormatBrl(decimal value) =>
        value.ToString("C", new System.Globalization.CultureInfo("pt-BR"));

    private static string TranslateStatus(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment => "Aguardando Pagamento",
        OrderStatus.Paid           => "Pago",
        OrderStatus.Validated      => "Validado",
        OrderStatus.Issued         => "Emitido",
        OrderStatus.Cancelled      => "Cancelado",
        OrderStatus.Expired        => "Expirado",
        _                          => status.ToString()
    };
}

// Stub enums — já existem no domínio real
public enum OrderStatus
{
    PendingPayment, Paid, Validated, Issued, Cancelled, Expired
}

// Extension helper para Task.WhenAll com desestruturação
public static class TaskExtensions
{
    public static async Task<(T1, T2)> WhenBoth<T1, T2>(this (Task<T1> t1, Task<T2> t2) tasks)
    {
        await Task.WhenAll(tasks.t1, tasks.t2);
        return (tasks.t1.Result, tasks.t2.Result);
    }
}
