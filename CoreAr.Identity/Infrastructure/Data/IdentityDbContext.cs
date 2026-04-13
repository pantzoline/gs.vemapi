using CoreAr.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoreAr.Identity.Infrastructure.Data;

/// <summary>
/// DbContext dedicado à identidade. Separado dos outros contextos (CRM, Ledger)
/// seguindo o princípio de responsabilidade única e bounded contexts do DDD.
/// </summary>
public class IdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<UserRefreshToken> RefreshTokens => Set<UserRefreshToken>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // OBRIGATÓRIO: configura as tabelas do Identity

        // ─── Renomeia tabelas para o schema corear_identity ───────────────────
        builder.HasDefaultSchema("identity");

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("Users");
            b.HasIndex(u => u.TenantId);          // Índice para queries por tenant
            b.HasIndex(u => u.DocumentNumber).IsUnique(); // CPF único no sistema
            b.Property(u => u.FullName).HasMaxLength(150).IsRequired();
            b.Property(u => u.DocumentNumber).HasMaxLength(14).IsRequired();
        });

        builder.Entity<ApplicationRole>(b => b.ToTable("Roles"));

        // ─── Configuração do Refresh Token ────────────────────────────────────
        builder.Entity<UserRefreshToken>(b =>
        {
            b.ToTable("UserRefreshTokens");
            b.HasKey(t => t.Id);

            // Índice no Hash para buscas rápidas no endpoint /refresh-token
            b.HasIndex(t => t.TokenHash).IsUnique();
            // Índice composto para buscar tokens ativos por usuário
            b.HasIndex(t => new { t.UserId, t.IsActive });

            // Coluna calculada IsActive (performance: evita WHERE com múltiplas condições)
            // Nota: Em PostgreSQL, usamos computed columns para isso
            b.Property(t => t.TokenHash).HasMaxLength(64).IsRequired(); // SHA-256 = 64 chars hex
            b.Property(t => t.DeviceFingerprint).HasMaxLength(256);
            b.Property(t => t.CreatedByIp).HasMaxLength(45); // IPv6 max length

            b.HasOne(t => t.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Seed das Roles base ──────────────────────────────────────────────
        builder.Entity<ApplicationRole>().HasData(
            new ApplicationRole { Id = Guid.Parse("00000001-0000-0000-0000-000000000001"),
                Name = "ROLE_MASTER", NormalizedName = "ROLE_MASTER" },
            new ApplicationRole { Id = Guid.Parse("00000001-0000-0000-0000-000000000002"),
                Name = "ROLE_ADMIN_AR", NormalizedName = "ROLE_ADMIN_AR" },
            new ApplicationRole { Id = Guid.Parse("00000001-0000-0000-0000-000000000003"),
                Name = "ROLE_PA", NormalizedName = "ROLE_PA" },
            new ApplicationRole { Id = Guid.Parse("00000001-0000-0000-0000-000000000004"),
                Name = "ROLE_AGR", NormalizedName = "ROLE_AGR" }
        );
    }
}

// Role customizada para suportar futuros campos (descrição, permissões granulares)
public class ApplicationRole : Microsoft.AspNetCore.Identity.IdentityRole<Guid>
{
    public string? Description { get; set; }
}
