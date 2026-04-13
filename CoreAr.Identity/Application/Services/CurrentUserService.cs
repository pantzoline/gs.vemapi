using CoreAr.Identity.Domain.Constants;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CoreAr.Identity.Application.Services;

/// <summary>
/// Serviço injetável que extrai os dados do usuário logado diretamente
/// dos Claims do JWT. Usado como fonte de verdade no DbContext por Global Query Filters.
///
/// Registro: services.AddHttpContextAccessor() + services.AddScoped<ICurrentUserService, CurrentUserService>()
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    Guid? ParentTenantId { get; }
    int TenantLevel { get; }
    bool IsMaster { get; }
    bool IsAdminAr { get; }
    bool IsPa { get; }
    string[] Roles { get; }
    bool IsAuthenticated { get; }
}

public class CurrentUserService : ICurrentUserService
{
    private readonly ClaimsPrincipal? _user;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _user = httpContextAccessor.HttpContext?.User;
    }

    public bool IsAuthenticated => _user?.Identity?.IsAuthenticated ?? false;

    public Guid UserId =>
        Guid.TryParse(_user?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id)
            ? id : Guid.Empty;

    public Guid TenantId =>
        Guid.TryParse(_user?.FindFirst(CustomClaims.TenantId)?.Value, out var id)
            ? id : Guid.Empty;

    public Guid? ParentTenantId
    {
        get
        {
            var val = _user?.FindFirst(CustomClaims.ParentTenantId)?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }

    public int TenantLevel =>
        int.TryParse(_user?.FindFirst(CustomClaims.TenantLevel)?.Value, out var level)
            ? level : 99;

    // Shortcuts para uso nos Query Filters
    public bool IsMaster   => Roles.Contains(Domain.Constants.Roles.Master);
    public bool IsAdminAr  => Roles.Contains(Domain.Constants.Roles.AdminAr);
    public bool IsPa       => Roles.Contains(Domain.Constants.Roles.Pa);

    public string[] Roles =>
        _user?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();
}
