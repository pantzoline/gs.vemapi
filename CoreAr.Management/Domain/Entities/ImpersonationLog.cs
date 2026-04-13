namespace CoreAr.Management.Domain.Entities;

/// <summary>
/// Log imutável de Impersonation — auditoria de quando o Master "entra"
/// na visão de outro Tenant para dar suporte.
///
/// SCHEMA SQL:
///
/// CREATE TABLE "ImpersonationLogs" (
///     "Id"                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
///     "MasterUserId"      UUID        NOT NULL,   -- Quem iniciou o impersonation
///     "ImpersonatedTenantId" UUID     NOT NULL REFERENCES "Tenants"("Id"),
///     "Reason"            TEXT        NOT NULL,   -- Motivo obrigatório
///     "StartedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
///     "EndedAt"           TIMESTAMPTZ,
///     "ActionsPerformed"  JSONB,                  -- Array de ações durante a sessão
///     "IpAddress"         VARCHAR(45)
/// );
///
/// CREATE INDEX "IX_ImpersonationLogs_MasterUserId"         ON "ImpersonationLogs"("MasterUserId");
/// CREATE INDEX "IX_ImpersonationLogs_ImpersonatedTenantId" ON "ImpersonationLogs"("ImpersonatedTenantId");
/// </summary>
public class ImpersonationLog
{
    public Guid Id { get; init; }
    public Guid MasterUserId { get; init; }
    public Guid ImpersonatedTenantId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; set; }
    public string? IpAddress { get; init; }

    // Snapshots de ações realizadas durante a sessão (JSONB)
    public List<string> ActionsPerformed { get; init; } = new();

    public bool IsActive => EndedAt == null;
    public TimeSpan? Duration => EndedAt.HasValue ? EndedAt.Value - StartedAt : null;
}
