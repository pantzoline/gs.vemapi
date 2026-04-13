using CoreAr.Crm.Application.Dashboard.DTOs;
using CoreAr.Crm.Application.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreAr.Crm.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize] // Qualquer usuário autenticado — o DashboardService filtra por Role internamente
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// Retorna os KPIs do topo do dashboard.
    /// O TenantId do JWT é aplicado automaticamente pelo DashboardService.
    /// Cache: 5 minutos no servidor (os dados de summary mudam com baixa frequência).
    /// </summary>
    [HttpGet("summary")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["from", "to"])]
    public async Task<ActionResult<DashboardSummaryDto>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var filter = BuildFilter(from, to);
        var result = await _dashboardService.GetSummaryAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>
    /// Retorna os dados para todos os gráficos.
    /// PartnerRanking retorna null para ROLE_PA/ROLE_AGR — o frontend esconde o widget.
    /// </summary>
    [HttpGet("charts")]
    [ResponseCache(Duration = 300, VaryByQueryKeys = ["from", "to", "granularity"])]
    public async Task<ActionResult<DashboardChartsDto>> GetCharts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] TimeGranularity granularity = TimeGranularity.Daily,
        CancellationToken ct = default)
    {
        var filter = BuildFilter(from, to) with { Granularity = granularity };
        var result = await _dashboardService.GetChartsAsync(filter, ct);
        return Ok(result);
    }

    // ─── Helper: Constrói o filtro com defaults seguros ───────────────────────
    private static DashboardFilterQuery BuildFilter(DateTime? from, DateTime? to)
    {
        var toDate   = to?.ToUniversalTime() ?? DateTime.UtcNow.Date.AddDays(1).AddSeconds(-1);
        var fromDate = from?.ToUniversalTime() ?? toDate.AddDays(-30);

        // Proteção: máximo de 366 dias para evitar queries de anos inteiros sem cache
        if ((toDate - fromDate).TotalDays > 366)
            fromDate = toDate.AddDays(-366);

        return new DashboardFilterQuery { From = fromDate, To = toDate };
    }
}
