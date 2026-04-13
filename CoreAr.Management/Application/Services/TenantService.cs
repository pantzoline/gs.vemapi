using CoreAr.Identity.Application.Services;
using CoreAr.Identity.Domain.Constants;
using CoreAr.Management.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CoreAr.Management.Application.Services;

public interface ITenantService
{
    Task<TenantHierarchyDto> GetHierarchyAsync(CancellationToken ct = default);
    Task<TenantDetailDto> GetByIdAsync(Guid tenantId, CancellationToken ct = default);
    Task<Tenant> CreateArAsync(CreateArRequest request, CancellationToken ct = default);
    Task<Tenant> CreatePaAsync(CreatePaRequest request, CancellationToken ct = default);
    Task UpdateBrandingAsync(Guid tenantId, UpdateBrandingRequest request, CancellationToken ct = default);
    Task SetContractAsync(Guid tenantId, SetContractRequest request, CancellationToken ct = default);
    Task<ImpersonationTokenDto> StartImpersonationAsync(Guid targetTenantId, string reason, CancellationToken ct = default);
    Task EndImpersonationAsync(Guid logId, CancellationToken ct = default);
}

public class TenantService : ITenantService
{
    private readonly ManagementDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly ILogger<TenantService> _logger;

    public TenantService(
        ManagementDbContext db,
        ICurrentUserService currentUser,
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        ILogger<TenantService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _logger = logger;
    }

    // ─── Hierarquia completa (para o TreeView) ────────────────────────────────
    public async Task<TenantHierarchyDto> GetHierarchyAsync(CancellationToken ct = default)
    {
        // Recupera apenas os Tenants do escopo do usuário atual
        // Master → vê tudo; AdminAr → vê apenas seus PAs; PA → vê apenas a si mesmo
        var query = _db.Tenants
            .AsNoTracking()
            .Include(t => t.Children.Where(c => c.IsActive))
                .ThenInclude(c => c.Children.Where(gc => gc.IsActive))
            .Include(t => t.Contracts.Where(c => c.IsActive));

        Tenant? root = _currentUser.IsMaster
            ? await query.FirstOrDefaultAsync(t => t.Level == TenantLevel.Master, ct)
            : await query.FirstOrDefaultAsync(t => t.Id == _currentUser.TenantId, ct);

        if (root == null)
            throw new NotFoundException("Tenant raiz não encontrado.");

        return MapToHierarchyDto(root);
    }

    // ─── Detalhe de um Tenant específico ─────────────────────────────────────
    public async Task<TenantDetailDto> GetByIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        await AssertAccessAsync(tenantId, ct);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Contracts)
            .Include(t => t.Parent)
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new NotFoundException($"Tenant {tenantId} não encontrado.");

        var userCount = await _db.Users
            .AsNoTracking()
            .CountAsync(u => u.TenantId == tenantId, ct);

        return new TenantDetailDto(tenant, userCount);
    }

    // ─── Criação de AR (apenas Master) ───────────────────────────────────────
    public async Task<Tenant> CreateArAsync(CreateArRequest req, CancellationToken ct = default)
    {
        if (!_currentUser.IsMaster)
            throw new ForbiddenException("Apenas o Master pode criar ARs.");

        await AssertDocumentUniqueAsync(req.Document, ct);

        var tenant = Tenant.CreateAr(
            req.Name, req.Document, req.Email,
            _currentUser.TenantId, _currentUser.UserId,
            req.EnabledAcProviders,
            new TenantBrandingInfo(null, null, null, null));

        _db.Tenants.Add(tenant);

        // Cria o usuário Admin da AR automaticamente
        await CreateAdminUserAsync(tenant, req.AdminEmail, req.AdminName, ct);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "AR criada: {TenantName} (CNPJ: {Document}) por {UserId}",
            req.Name, req.Document, _currentUser.UserId);

        return tenant;
    }

    // ─── Criação de PA (Master ou Admin AR) ───────────────────────────────────
    public async Task<Tenant> CreatePaAsync(CreatePaRequest req, CancellationToken ct = default)
    {
        // Valida que o criador tem acesso à AR pai
        await AssertAccessAsync(req.ParentArId, ct);

        var parentAr = await _db.Tenants
            .FirstOrDefaultAsync(t => t.Id == req.ParentArId && t.Level == TenantLevel.AuthorityRegistrar, ct)
            ?? throw new NotFoundException("AR pai não encontrada.");

        if (!parentAr.IsActive)
            throw new DomainException("Não é possível criar PAs em uma AR inativa.");

        await AssertDocumentUniqueAsync(req.Document, ct);

        // PA herda as ACs da AR (só pode usar o que a AR tem permissão)
        var enabledAcs = req.EnabledAcProviders
            .Intersect(parentAr.EnabledAcProviders, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tenant = Tenant.CreatePa(
            req.Name, req.Document, req.Email,
            req.ParentArId, _currentUser.UserId,
            enabledAcs,
            new TenantBrandingInfo(null, null, null, null));

        _db.Tenants.Add(tenant);

        await CreateAdminUserAsync(tenant, req.AdminEmail, req.AdminName, ct);

        await _db.SaveChangesAsync(ct);

        return tenant;
    }

    // ─── Atualização de Branding (White Label) ────────────────────────────────
    public async Task UpdateBrandingAsync(
        Guid tenantId, UpdateBrandingRequest req, CancellationToken ct = default)
    {
        await AssertAccessAsync(tenantId, ct);

        var tenant = await _db.Tenants.FindAsync([tenantId], ct)
            ?? throw new NotFoundException($"Tenant {tenantId} não encontrado.");

        tenant.UpdateBranding(req.LogoUrl, req.PrimaryColor, req.SecondaryColor, req.AccentColor);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Contrato de Comissão ─────────────────────────────────────────────────
    public async Task SetContractAsync(
        Guid tenantId, SetContractRequest req, CancellationToken ct = default)
    {
        // Apenas Master e AdminAR podem configurar contratos
        if (!_currentUser.IsMaster && !_currentUser.IsAdminAr)
            throw new ForbiddenException("Sem permissão para gerenciar contratos.");

        await AssertAccessAsync(tenantId, ct);

        // Desativa contratos existentes para o mesmo produto/AC
        var existing = await _db.PartnerContracts
            .Where(c => c.TenantId == tenantId &&
                        c.ProductCode == req.ProductCode &&
                        c.AcProvider == req.AcProvider &&
                        c.IsActive)
            .ToListAsync(ct);

        foreach (var c in existing) c.Deactivate();

        var newContract = PartnerContract.Create(
            tenantId, req.ProductCode, req.CommissionType,
            req.CommissionValue, _currentUser.UserId,
            req.AcProvider, req.ValidUntil);

        _db.PartnerContracts.Add(newContract);
        await _db.SaveChangesAsync(ct);
    }

    // ─── Impersonation Mode ───────────────────────────────────────────────────
    public async Task<ImpersonationTokenDto> StartImpersonationAsync(
        Guid targetTenantId, string reason, CancellationToken ct = default)
    {
        if (!_currentUser.IsMaster)
            throw new ForbiddenException("Apenas o Master pode usar Impersonation Mode.");

        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 10)
            throw new DomainException("Motivo do impersonation deve ter pelo menos 10 caracteres.");

        var targetTenant = await _db.Tenants.FindAsync([targetTenantId], ct)
            ?? throw new NotFoundException($"Tenant {targetTenantId} não encontrado.");

        // Cria log de auditoria antes de gerar o token
        var log = new ImpersonationLog
        {
            Id = Guid.NewGuid(),
            MasterUserId = _currentUser.UserId,
            ImpersonatedTenantId = targetTenantId,
            Reason = reason,
            StartedAt = DateTime.UtcNow,
            IpAddress = _currentUser.RemoteIpAddress,
        };

        _db.ImpersonationLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        // Gera um JWT especial com TenantId do alvo MAS com claim "IsImpersonating"
        // O frontend exibe um banner de "MODO SUPORTE ATIVO"
        var impersonationToken = await _tokenService.GenerateImpersonationTokenAsync(
            _currentUser.UserId, targetTenantId, log.Id);

        _logger.LogWarning(
            "IMPERSONATION INICIADO: Master={MasterUserId} → Tenant={TargetTenantId} " +
            "Motivo='{Reason}' LogId={LogId}",
            _currentUser.UserId, targetTenantId, reason, log.Id);

        return new ImpersonationTokenDto(impersonationToken, log.Id, targetTenant.Name);
    }

    public async Task EndImpersonationAsync(Guid logId, CancellationToken ct = default)
    {
        var log = await _db.ImpersonationLogs.FindAsync([logId], ct);
        if (log is { IsActive: true })
        {
            log.EndedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogWarning(
            "IMPERSONATION ENCERRADO: LogId={LogId} Duração={Duration}",
            logId, log?.Duration?.ToString("mm\\:ss") ?? "N/A");
    }

    // ─── Privados ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifica se o usuário atual tem acesso para gerenciar o tenantId alvo.
    /// Master → acessa qualquer Tenant.
    /// AdminAR → acessa apenas seu Tenant e seus PAs filhos.
    /// PA → acessa apenas a si mesmo.
    /// </summary>
    private async Task AssertAccessAsync(Guid targetTenantId, CancellationToken ct)
    {
        if (_currentUser.IsMaster) return;

        var isOwnTenant = targetTenantId == _currentUser.TenantId;
        if (isOwnTenant) return;

        // Verifica se é filho direto
        var isChild = await _db.Tenants
            .AsNoTracking()
            .AnyAsync(t => t.Id == targetTenantId && t.ParentId == _currentUser.TenantId, ct);

        if (!isChild)
            throw new ForbiddenException(
                $"Sem permissão para acessar o Tenant {targetTenantId}.");
    }

    private async Task AssertDocumentUniqueAsync(string document, CancellationToken ct)
    {
        if (await _db.Tenants.AnyAsync(t => t.Document == document, ct))
            throw new DomainException($"CNPJ {document} já está cadastrado no sistema.");
    }

    private async Task CreateAdminUserAsync(
        Tenant tenant, string adminEmail, string adminName, CancellationToken ct)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = false,
            TenantId = tenant.Id,
            TenantLevel = tenant.Level.ToString(),
            FullName = adminName,
            IsActive = true,
        };

        // Gera senha temporária forte
        var tempPassword = GenerateSecurePassword();
        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            throw new DomainException($"Falha ao criar usuário admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        // Atribui a Role de Admin para o nível do Tenant
        var role = tenant.Level switch
        {
            TenantLevel.AuthorityRegistrar => Roles.AdminAr,
            TenantLevel.PointOfAttendance  => Roles.Pa,
            _ => Roles.AGR,
        };
        await _userManager.AddToRoleAsync(user, role);

        // Gera token de confirmação de email e definição de senha
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // Envia e-mail de boas-vindas (fire-and-forget com log de erro)
        _ = _emailService.SendWelcomeEmailAsync(adminEmail, adminName, tenant.Name, token)
            .ContinueWith(t =>
            {
                if (t.IsFaulted)
                    _logger.LogError(t.Exception, "Falha ao enviar e-mail de boas-vindas para {Email}", adminEmail);
            }, CancellationToken.None);
    }

    private static string GenerateSecurePassword()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
        return new string(Enumerable.Range(0, 16)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }

    private static TenantHierarchyDto MapToHierarchyDto(Tenant t) =>
        new(t.Id, t.Name, t.Document, t.Level.ToString(), t.IsActive,
            t.PrimaryColor, t.LogoUrl,
            t.Children.Select(MapToHierarchyDto).ToList(),
            t.Contracts.Count);
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record TenantHierarchyDto(
    Guid Id, string Name, string Document, string Level,
    bool IsActive, string PrimaryColor, string? LogoUrl,
    IReadOnlyList<TenantHierarchyDto> Children,
    int ContractCount);

public record TenantDetailDto(Tenant Tenant, int UserCount);
public record ImpersonationTokenDto(string Token, Guid LogId, string TenantName);

public record CreateArRequest(
    string Name, string Document, string Email,
    string AdminName, string AdminEmail,
    List<string> EnabledAcProviders);

public record CreatePaRequest(
    string Name, string Document, string Email,
    Guid ParentArId, string AdminName, string AdminEmail,
    List<string> EnabledAcProviders);

public record UpdateBrandingRequest(
    string? LogoUrl, string? PrimaryColor,
    string? SecondaryColor, string? AccentColor);

public record SetContractRequest(
    string ProductCode, CommissionType CommissionType,
    decimal CommissionValue, string? AcProvider, DateTime? ValidUntil);

// Stubs para compilação
public class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser<Guid>
{
    public Guid TenantId { get; set; }
    public string TenantLevel { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
public interface ITokenService { Task<string> GenerateImpersonationTokenAsync(Guid masterUserId, Guid targetTenantId, Guid logId); }
public interface IEmailService { Task SendWelcomeEmailAsync(string email, string name, string tenantName, string token); }
public class NotFoundException(string msg) : Exception(msg);
public class ForbiddenException(string msg) : Exception(msg);
