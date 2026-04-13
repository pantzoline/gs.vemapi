namespace CoreAr.Crm.Application.Dashboard.DTOs;

// ─── Resposta do endpoint /api/dashboard/summary ─────────────────────────────

/// <summary>
/// KPIs do topo do dashboard. Dados 100% agregados no servidor.
/// Nunca inclui transações individuais do Ledger — apenas totais.
/// </summary>
public record DashboardSummaryDto
{
    // Cards de KPI
    public KpiCardDto TotalSales { get; init; } = null!;
    public KpiCardDto NetRevenue { get; init; } = null!;      // Após split de comissões
    public KpiCardDto AverageTicket { get; init; } = null!;
    public KpiCardDto ProtocolConversion { get; init; } = null!; // % Pedidos → Emitidos

    // Metadados do contexto (para o frontend saber o que exibir)
    public string TenantName { get; init; } = string.Empty;
    public string TenantLevel { get; init; } = string.Empty;
    public DateRangeDto Period { get; init; } = null!;
}

public record KpiCardDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string FormattedValue { get; init; } = string.Empty;  // Já formatado: "R$ 12.450,00"
    public decimal? ChangePercent { get; init; }   // Variação vs. período anterior (+12.5%)
    public TrendDirection Trend { get; init; }
    public string? Unit { get; init; }             // "%" para conversão, "R$" para valores
}

public record DateRangeDto(DateTime From, DateTime To);
public enum TrendDirection { Up, Down, Neutral }

// ─── Resposta do endpoint /api/dashboard/charts ──────────────────────────────

public record DashboardChartsDto
{
    /// <summary>
    /// Série temporal para o AreaChart (Vendas por dia/hora).
    /// Formatada para consumo direto pelo Recharts sem transformação.
    /// </summary>
    public IReadOnlyList<SalesTimeSeriesDto> SalesTimeSeries { get; init; } = [];

    /// <summary>
    /// Distribuição de pedidos por status para o PieChart.
    /// </summary>
    public IReadOnlyList<OrderStatusDistributionDto> StatusDistribution { get; init; } = [];

    /// <summary>
    /// Top 5 parceiros por volume. NULL para ROLE_PA e ROLE_AGR.
    /// O backend retorna null; o frontend esconde o widget.
    /// </summary>
    public IReadOnlyList<PartnerRankingDto>? PartnerRanking { get; init; }

    /// <summary>
    /// Receita por produto (e-CPF A1, A3, e-CNPJ, etc.)
    /// </summary>
    public IReadOnlyList<RevenueByProductDto> RevenueByProduct { get; init; } = [];
}

public record SalesTimeSeriesDto
{
    public string Date { get; init; } = string.Empty;       // "2025-01-15" (ISO 8601)
    public decimal TotalRevenue { get; init; }              // Receita bruta do período
    public decimal NetRevenue { get; init; }                // Após comissões
    public int OrderCount { get; init; }                    // Quantidade de pedidos
}

public record OrderStatusDistributionDto
{
    public string Status { get; init; } = string.Empty;     // "Aguardando Pagamento"
    public string StatusKey { get; init; } = string.Empty;  // "PENDING_PAYMENT" (para i18n)
    public int Count { get; init; }
    public decimal Percentage { get; init; }
    public string Color { get; init; } = string.Empty;      // Cor HEX para o PieChart
}

public record PartnerRankingDto
{
    public int Rank { get; init; }
    public Guid PaId { get; init; }
    public string PaName { get; init; } = string.Empty;
    public string ArName { get; init; } = string.Empty;
    public int TotalOrders { get; init; }
    public decimal TotalRevenue { get; init; }
    public decimal ChangePercent { get; init; }
}

public record RevenueByProductDto
{
    public string ProductName { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
    public int Quantity { get; init; }
}

// ─── Query parameters para filtro de período ─────────────────────────────────
public record DashboardFilterQuery
{
    public DateTime From { get; init; } = DateTime.UtcNow.AddDays(-30);
    public DateTime To { get; init; } = DateTime.UtcNow;
    public TimeGranularity Granularity { get; init; } = TimeGranularity.Daily;
}

public enum TimeGranularity { Hourly, Daily, Weekly, Monthly }
