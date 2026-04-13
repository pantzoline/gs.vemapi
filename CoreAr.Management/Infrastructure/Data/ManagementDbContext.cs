using CoreAr.Management.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoreAr.Management.Infrastructure.Data;

public class ManagementDbContext : DbContext
{
    public ManagementDbContext(DbContextOptions<ManagementDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<PartnerContract> PartnerContracts => Set<PartnerContract>();
    public DbSet<ImpersonationLog> ImpersonationLogs => Set<ImpersonationLog>();

    // Stub para permitir filtro/join com Users
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ─── Tenant ───────────────────────────────────────────────────────────
        builder.Entity<Tenant>(b =>
        {
            b.ToTable("Tenants");
            b.HasKey(t => t.Id);

            b.HasIndex(t => t.Document).IsUnique();
            b.HasIndex(t => t.Level);
            b.HasIndex(t => t.ParentId);

            b.Property(t => t.Name).IsRequired().HasMaxLength(200);
            b.Property(t => t.TradeName).HasMaxLength(200);
            b.Property(t => t.Document).IsRequired().HasMaxLength(14);
            b.Property(t => t.Email).IsRequired().HasMaxLength(254);
            b.Property(t => t.PrimaryColor).HasMaxLength(7);
            
            // Relacionamento Hierárquico
            b.HasOne(t => t.Parent)
             .WithMany(t => t.Children)
             .HasForeignKey(t => t.ParentId)
             .OnDelete(DeleteBehavior.Restrict);

            // Mapeamento PostgreSQL Array (EF Core 8)
            b.Property(t => t.EnabledAcProviders).HasColumnType("text[]");
        });

        // ─── PartnerContract ──────────────────────────────────────────────────
        builder.Entity<PartnerContract>(b =>
        {
            b.ToTable("PartnerContracts");
            b.HasKey(c => c.Id);

            b.HasOne(c => c.Tenant)
             .WithMany(t => t.Contracts)
             .HasForeignKey(c => c.TenantId)
             .OnDelete(DeleteBehavior.Cascade);

            b.Property(c => c.ProductCode).IsRequired().HasMaxLength(50);
            b.Property(c => c.CommissionValue).HasColumnType("decimal(18,4)");

            // Unique Constraint de contrato ativo
            b.HasIndex(c => new { c.TenantId, c.ProductCode, c.AcProvider, c.IsActive }).IsUnique();
        });

        // ─── ImpersonationLog ─────────────────────────────────────────────────
        builder.Entity<ImpersonationLog>(b =>
        {
            b.ToTable("ImpersonationLogs");
            b.HasKey(l => l.Id);
            
            b.HasIndex(l => l.MasterUserId);
            b.HasIndex(l => l.ImpersonatedTenantId);

            // Armazena JSONB np PostgreSQL
            b.Property(l => l.ActionsPerformed).HasColumnType("jsonb");
        });
        
        // Mapeamento Stub do Identity User
        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("AspNetUsers");
            b.HasKey(u => u.Id);
        });
    }
}

// Stub do ApplicationUser para o DbContext local
public class ApplicationUser 
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
}
