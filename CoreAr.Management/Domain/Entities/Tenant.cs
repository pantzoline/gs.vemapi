namespace CoreAr.Management.Domain.Entities;

/// <summary>
/// Entidade central da hierarquia multi-tenant.
///
/// SCHEMA SQL COMPLETO:
///
/// CREATE TABLE "Tenants" (
///     "Id"            UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
///     "Name"          VARCHAR(200)    NOT NULL,
///     "TradeName"     VARCHAR(200),
///     "Document"      VARCHAR(14)     NOT NULL UNIQUE,   -- CNPJ sem formatação
///     "Level"         SMALLINT        NOT NULL,          -- 0=Master,1=AR,2=PA,3=AGR
///     "ParentId"      UUID            REFERENCES "Tenants"("Id"),
///     "IsActive"      BOOLEAN         NOT NULL DEFAULT TRUE,
///
///     -- Contato
///     "Email"         VARCHAR(254)    NOT NULL,
///     "Phone"         VARCHAR(11),
///
///     -- Endereço
///     "ZipCode"       VARCHAR(8),
///     "State"         VARCHAR(2),
///     "City"          VARCHAR(100),
///     "Street"        VARCHAR(200),
///     "Number"        VARCHAR(10),
///
///     -- White Label
///     "LogoUrl"       TEXT,
///     "PrimaryColor"  VARCHAR(7)      DEFAULT '#3b82f6',   -- HEX
///     "SecondaryColor" VARCHAR(7)     DEFAULT '#10b981',
///     "AccentColor"   VARCHAR(7)      DEFAULT '#8b5cf6',
///
///     -- ACs habilitadas para este parceiro (array PostgreSQL)
///     "EnabledAcProviders" TEXT[]     NOT NULL DEFAULT '{}',
///
///     -- Auditoria
///     "CreatedAt"     TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
///     "CreatedByUserId" UUID          NOT NULL,
///     "UpdatedAt"     TIMESTAMPTZ     NOT NULL DEFAULT NOW()
/// );
///
/// -- Índices
/// CREATE INDEX "IX_Tenants_ParentId"    ON "Tenants"("ParentId");
/// CREATE INDEX "IX_Tenants_Level"       ON "Tenants"("Level");
/// CREATE UNIQUE INDEX "IX_Tenants_Document" ON "Tenants"("Document");
///
/// -- Constraint: PA deve ter ParentId (AR), AGR deve ter ParentId (PA)
/// ALTER TABLE "Tenants" ADD CONSTRAINT "CK_Tenants_ParentRequired"
///   CHECK (("Level" = 0) OR ("ParentId" IS NOT NULL));
///
/// -- Constraint: Master não pode ter pai
/// ALTER TABLE "Tenants" ADD CONSTRAINT "CK_Tenants_MasterNoParent"
///   CHECK (("Level" != 0) OR ("ParentId" IS NULL));
/// </summary>
public class Tenant
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }            // Nome Fantasia
    public string Document { get; private set; } = string.Empty; // CNPJ (14 dígitos)
    public TenantLevel Level { get; private set; }

    // ─── Hierarquia ───────────────────────────────────────────────────────────
    public Guid? ParentId { get; private set; }
    public Tenant? Parent { get; private set; }
    private readonly List<Tenant> _children = new();
    public IReadOnlyCollection<Tenant> Children => _children.AsReadOnly();

    // ─── Contratos de Split vinculados ───────────────────────────────────────
    private readonly List<PartnerContract> _contracts = new();
    public IReadOnlyCollection<PartnerContract> Contracts => _contracts.AsReadOnly();

    // ─── Status e Contato ─────────────────────────────────────────────────────
    public bool IsActive { get; private set; } = true;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }

    // ─── Endereço ─────────────────────────────────────────────────────────────
    public string? ZipCode { get; private set; }
    public string? State { get; private set; }
    public string? City { get; private set; }
    public string? Street { get; private set; }
    public string? Number { get; private set; }

    // ─── White Label ──────────────────────────────────────────────────────────
    public string? LogoUrl { get; private set; }
    public string PrimaryColor { get; private set; } = "#3b82f6";
    public string SecondaryColor { get; private set; } = "#10b981";
    public string AccentColor { get; private set; } = "#8b5cf6";

    // ─── ACs habilitadas para este Tenant ─────────────────────────────────────
    // Um PA só pode emitir na AC que sua AR liberou
    public List<string> EnabledAcProviders { get; private set; } = new();

    // ─── Impersonation Log (auditoria) ────────────────────────────────────────
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // ─── Factory Methods ───────────────────────────────────────────────────────
    public static Tenant CreateMaster(
        string name, string document, string email,
        Guid createdByUserId, TenantBrandingInfo? branding = null)
    {
        return Create(name, document, email, TenantLevel.Master, null, createdByUserId, branding);
    }

    public static Tenant CreateAr(
        string name, string document, string email,
        Guid masterTenantId, Guid createdByUserId,
        List<string>? enabledAcs = null, TenantBrandingInfo? branding = null)
    {
        var ar = Create(name, document, email, TenantLevel.AuthorityRegistrar,
            masterTenantId, createdByUserId, branding);
        ar.EnabledAcProviders = enabledAcs ?? new();
        return ar;
    }

    public static Tenant CreatePa(
        string name, string document, string email,
        Guid arTenantId, Guid createdByUserId,
        List<string>? enabledAcs = null, TenantBrandingInfo? branding = null)
    {
        var pa = Create(name, document, email, TenantLevel.PointOfAttendance,
            arTenantId, createdByUserId, branding);
        pa.EnabledAcProviders = enabledAcs ?? new();
        return pa;
    }

    // ─── Mutações controladas ─────────────────────────────────────────────────
    public void UpdateBranding(string? logoUrl, string? primary, string? secondary, string? accent)
    {
        LogoUrl = logoUrl ?? LogoUrl;
        PrimaryColor = primary ?? PrimaryColor;
        SecondaryColor = secondary ?? SecondaryColor;
        AccentColor = accent ?? AccentColor;
        Touch();
    }

    public void UpdateContact(string email, string? phone)
    {
        Email = email;
        Phone = phone;
        Touch();
    }

    public void UpdateAddress(string zipCode, string state, string city, string street, string number)
    {
        ZipCode = zipCode; State = state; City = city;
        Street = street; Number = number;
        Touch();
    }

    public void SetEnabledAcProviders(List<string> providers)
    {
        EnabledAcProviders = providers;
        Touch();
    }

    public void Deactivate() { IsActive = false; Touch(); }
    public void Reactivate() { IsActive = true; Touch(); }

    // ─── Privados ─────────────────────────────────────────────────────────────
    private static Tenant Create(
        string name, string document, string email,
        TenantLevel level, Guid? parentId, Guid createdByUserId,
        TenantBrandingInfo? branding)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Nome do parceiro é obrigatório.");
        if (document.Length != 14 || !document.All(char.IsDigit))
            throw new DomainException("CNPJ deve conter exatamente 14 dígitos.");

        var t = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name, Document = document, Email = email,
            Level = level, ParentId = parentId,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        if (branding != null)
        {
            t.LogoUrl = branding.LogoUrl;
            t.PrimaryColor = branding.PrimaryColor ?? "#3b82f6";
            t.SecondaryColor = branding.SecondaryColor ?? "#10b981";
            t.AccentColor = branding.AccentColor ?? "#8b5cf6";
        }

        return t;
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}

public enum TenantLevel
{
    Master = 0,
    AuthorityRegistrar = 1,
    PointOfAttendance = 2,
    Agent = 3
}

public record TenantBrandingInfo(
    string? LogoUrl,
    string? PrimaryColor,
    string? SecondaryColor,
    string? AccentColor);

public class DomainException(string message) : Exception(message);
