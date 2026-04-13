namespace CoreAr.Management.Domain.Entities;

/// <summary>
/// Contrato de comissionamento entre a Matriz e um parceiro.
/// Define quanto (% ou valor fixo) um Tenant recebe por produto emitido.
///
/// SCHEMA SQL:
///
/// CREATE TABLE "PartnerContracts" (
///     "Id"              UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
///     "TenantId"        UUID          NOT NULL REFERENCES "Tenants"("Id"),
///     "ProductCode"     VARCHAR(50)   NOT NULL,   -- Ex: "E-CPF-A3-1ANO" ou "*" para todos
///     "CommissionType"  SMALLINT      NOT NULL,   -- 0=Percent, 1=FixedCents
///     "CommissionValue" DECIMAL(18,4) NOT NULL,   -- % (ex: 30.5000) ou centavos (ex: 5000)
///     "AcProvider"      VARCHAR(20),              -- NULL = aplica a todas as ACs
///     "IsActive"        BOOLEAN       NOT NULL DEFAULT TRUE,
///     "ValidFrom"       DATE          NOT NULL DEFAULT CURRENT_DATE,
///     "ValidUntil"      DATE,                     -- NULL = sem expiração
///     "CreatedAt"       TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
///     "CreatedByUserId" UUID          NOT NULL,
///
///     -- Garante que não há dois contratos ativos para o mesmo produto/AC no mesmo tenant
///     UNIQUE("TenantId", "ProductCode", "AcProvider", "IsActive")
/// );
///
/// CREATE INDEX "IX_PartnerContracts_TenantId"    ON "PartnerContracts"("TenantId");
/// CREATE INDEX "IX_PartnerContracts_ProductCode" ON "PartnerContracts"("ProductCode");
/// </summary>
public class PartnerContract
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;

    /// <summary>
    /// Código do produto ("E-CPF-A3-1ANO") ou "*" para aplicar a todos os produtos.
    /// </summary>
    public string ProductCode { get; private set; } = string.Empty;

    public CommissionType CommissionType { get; private set; }

    /// <summary>
    /// Se Percent: valor 0-100 (ex: 30.5 = 30,5%).
    /// Se FixedCents: valor em centavos (ex: 5000 = R$ 50,00).
    /// </summary>
    public decimal CommissionValue { get; private set; }

    /// <summary>
    /// AC específica ou null para todas.
    /// Permite: "SYNGULAR ganha 35%, VALID ganha 30%"
    /// </summary>
    public string? AcProvider { get; private set; }

    public bool IsActive { get; private set; } = true;
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ─── Computed: calcula o valor da comissão dado um total ─────────────────
    public long CalculateCommissionCents(long totalAmountInCents)
    {
        return CommissionType switch
        {
            CommissionType.Percent =>
                // Usa aritmética inteira para evitar ponto flutuante
                (long)Math.Floor(totalAmountInCents * (double)CommissionValue / 100.0),
            CommissionType.FixedCents =>
                Math.Min((long)CommissionValue, totalAmountInCents),
            _ => 0L
        };
    }

    public bool IsValidOn(DateTime date) =>
        IsActive && date >= ValidFrom && (ValidUntil == null || date <= ValidUntil);

    // ─── Factory ──────────────────────────────────────────────────────────────
    public static PartnerContract Create(
        Guid tenantId, string productCode, CommissionType type,
        decimal value, Guid createdBy,
        string? acProvider = null, DateTime? validUntil = null)
    {
        if (type == CommissionType.Percent && (value < 0 || value > 100))
            throw new DomainException("Percentual de comissão deve ser entre 0% e 100%.");
        if (type == CommissionType.FixedCents && value < 0)
            throw new DomainException("Valor fixo de comissão não pode ser negativo.");

        return new PartnerContract
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductCode = productCode,
            CommissionType = type,
            CommissionValue = value,
            AcProvider = acProvider,
            CreatedByUserId = createdBy,
            CreatedAt = DateTime.UtcNow,
            ValidFrom = DateTime.UtcNow.Date,
            ValidUntil = validUntil,
        };
    }

    public void Deactivate() => IsActive = false;
}

public enum CommissionType { Percent = 0, FixedCents = 1 }
