using Microsoft.AspNetCore.Identity;

namespace CoreAr.Identity.Domain.Entities;

/// <summary>
/// Entidade de usuário do sistema. Extende o Identity com campos de negócio
/// específicos para o modelo Multi-Tenant de Certificação Digital.
/// Usa Guid como chave primária (obrigatório para sistemas distribuídos).
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    // ─── Dados Pessoais ──────────────────────────────────────────────────────
    public string FullName { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty; // CPF (sempre)

    // ─── Hierarquia Multi-Tenant ─────────────────────────────────────────────
    // A Role define o CARGO. O TenantId define a CASA.
    // Um usuário ROLE_PA com TenantId = PA-001 só enxerga dados do PA-001.
    public Guid TenantId { get; set; }         // Id da AR, PA, ou Guid.Empty p/ Master
    public TenantLevel TenantLevel { get; set; } // Master, AR, PA, AGR

    // Referência para o nó pai na hierarquia (para navegação UP)
    public Guid? ParentTenantId { get; set; }  // O AR que "dono" desse PA, por ex.

    // ─── Auditoria e Segurança ───────────────────────────────────────────────
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Controle de tentativas falhas (Account Lockout)
    // O próprio Identity gerencia AccessFailedCount e LockoutEnd via IdentityUser

    // ─── Refresh Tokens (relacionamento) ─────────────────────────────────────
    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
}

public enum TenantLevel
{
    Master = 0,             // Acesso total — Administrador do sistema
    AuthorityRegistrar = 1, // AR — Vê todos os seus PAs
    PointOfAttendance = 2,  // PA — Vê apenas seus próprios pedidos
    Agent = 3               // AGR — Apenas lança e valida pedidos
}
