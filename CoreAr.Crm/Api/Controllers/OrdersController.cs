using CoreAr.Crm.Application.Orders.Services;
using CoreAr.Crm.Domain.Entities;
using CoreAr.Identity.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreAr.Crm.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // GET /api/orders?page=1&pageSize=20&search=joao&statuses=Paid,Issued
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = true,
        [FromQuery] string? statuses = null,      // "Paid,Issued" → split
        [FromQuery] string? acProviders = null,
        [FromQuery] string? certTypes = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken ct = default)
    {
        var query = new OrderListQuery
        {
            Page = Math.Clamp(page, 1, 1000),
            PageSize = Math.Clamp(pageSize, 5, 100),
            Search = search,
            SortBy = sortBy,
            SortDescending = sortDesc,
            From = from?.ToUniversalTime(),
            To = to?.ToUniversalTime(),
            // Parse dos filtros multi-valor vindos como string CSV
            Statuses = ParseEnumList<OrderStatus>(statuses),
            AcProviders = acProviders?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            CertificationTypes = ParseEnumList<CertificationType>(certTypes),
        };

        var result = await _orderService.GetPagedAsync(query, ct);
        return Ok(result);
    }

    // GET /api/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var order = await _orderService.GetByIdAsync(id, ct);
        return Ok(order);
    }

    // GET /api/orders/{id}/live — inclui consulta em tempo real à AC
    [HttpGet("{id:guid}/live")]
    public async Task<IActionResult> GetByIdLive(Guid id, CancellationToken ct)
    {
        var order = await _orderService.GetByIdWithAcStatusAsync(id, ct);
        return Ok(order);
    }

    // POST /api/orders/{id}/transition
    [HttpPost("{id:guid}/transition")]
    [Authorize(Roles = $"{Roles.Master},{Roles.AdminAr},{Roles.Pa}")]
    public async Task<IActionResult> TransitionStatus(
        Guid id,
        [FromBody] TransitionStatusRequest request,
        CancellationToken ct)
    {
        await _orderService.TransitionStatusAsync(id, request.Status, request.Description, null, ct);
        return Ok(new { message = "Status atualizado com sucesso." });
    }

    // POST /api/orders/{id}/resend-payment-link
    [HttpPost("{id:guid}/resend-payment-link")]
    public async Task<IActionResult> ResendPaymentLink(Guid id, CancellationToken ct)
    {
        await _orderService.ResendPaymentLinkAsync(id, ct);
        return Ok(new { message = "Link de pagamento reenviado." });
    }

    private static List<T>? ParseEnumList<T>(string? csv) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<T>(s.Trim(), true, out var val) ? (T?)val : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
    }
}

public record TransitionStatusRequest(OrderStatus Status, string Description);
