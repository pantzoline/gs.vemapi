using CoreAr.Management.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoreAr.Management.Api.Controllers;

[ApiController]
[Route("api/management/[controller]")]
[Authorize] // Requer autenticação por padrão
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet("hierarchy")]
    public async Task<IActionResult> GetHierarchy(CancellationToken ct)
    {
        var hierarchy = await _tenantService.GetHierarchyAsync(ct);
        return Ok(hierarchy);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var detail = await _tenantService.GetByIdAsync(id, ct);
        return Ok(detail);
    }

    // Role exigida configurada na rota, ou avaliada internamente no service.
    [HttpPost("ar")]
    [Authorize(Roles = "ROLE_MASTER")] // Apenas Master
    public async Task<IActionResult> CreateAr([FromBody] CreateArRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.CreateArAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPost("pa")]
    [Authorize(Roles = "ROLE_MASTER,ROLE_ADMIN_AR")] // Master ou Admin AR
    public async Task<IActionResult> CreatePa([FromBody] CreatePaRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.CreatePaAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}/branding")]
    public async Task<IActionResult> UpdateBranding(Guid id, [FromBody] UpdateBrandingRequest request, CancellationToken ct)
    {
        await _tenantService.UpdateBrandingAsync(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/contracts")]
    public async Task<IActionResult> SetContract(Guid id, [FromBody] SetContractRequest request, CancellationToken ct)
    {
        await _tenantService.SetContractAsync(id, request, ct);
        return Ok();
    }

    [HttpPost("impersonate/{targetTenantId:guid}")]
    [Authorize(Roles = "ROLE_MASTER")]
    public async Task<IActionResult> StartImpersonation(Guid targetTenantId, [FromBody] ImpersonationRequest request, CancellationToken ct)
    {
        var result = await _tenantService.StartImpersonationAsync(targetTenantId, request.Reason, ct);
        return Ok(result);
    }

    [HttpPost("impersonate/end/{logId:guid}")]
    [Authorize(Roles = "ROLE_MASTER")]
    public async Task<IActionResult> EndImpersonation(Guid logId, CancellationToken ct)
    {
        await _tenantService.EndImpersonationAsync(logId, ct);
        return NoContent();
    }
}

public record ImpersonationRequest(string Reason);
