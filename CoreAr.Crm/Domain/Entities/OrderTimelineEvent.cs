namespace CoreAr.Crm.Domain.Entities;

/// <summary>
/// Evento de Timeline do Pedido — IMUTÁVEL.
/// 
/// Funciona como Event Sourcing parcial: cada mudança de status ou ação
/// relevante no pedido gera um evento que nunca é deletado ou modificado.
/// 
/// SCHEMA SQL:
/// CREATE TABLE "OrderTimelineEvents" (
///     "Id"                UUID        PRIMARY KEY,
///     "OrderId"           UUID        NOT NULL REFERENCES "Orders"("Id"),
///     "Type"              VARCHAR(50) NOT NULL,
///     "Title"             VARCHAR(200) NOT NULL,
///     "Description"       TEXT        NOT NULL,
///     "AdditionalData"    JSONB,          -- Dados extras (ex: resposta bruta da AC)
///     "IsError"           BOOLEAN     NOT NULL DEFAULT FALSE,
///     "TriggeredByUserId" UUID        NOT NULL,
///     "OccurredAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW()
/// );
/// 
/// -- Índices críticos para performance da Timeline
/// CREATE INDEX "IX_Timeline_OrderId" ON "OrderTimelineEvents"("OrderId", "OccurredAt" DESC);
/// CREATE INDEX "IX_Timeline_Type"    ON "OrderTimelineEvents"("Type");
/// 
/// -- IMUTABILIDADE: Trigger que impede UPDATE/DELETE
/// CREATE TRIGGER trg_timeline_immutable
///     BEFORE UPDATE OR DELETE ON "OrderTimelineEvents"
///     FOR EACH ROW EXECUTE FUNCTION prevent_ledger_mutation(); -- mesmo fn do Ledger
/// </summary>
public class OrderTimelineEvent
{
    public Guid Id { get; init; }
    public Guid OrderId { get; init; }

    // Tipo estruturado (para filtros e iconografia no frontend)
    public OrderTimelineEventType Type { get; init; }

    // Textos exibidos na UI
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;

    // JSON com informações extras (ex: payload da AC, CPF validado, motivo de rejeição)
    public string? AdditionalData { get; init; }

    // Nó vermelho na timeline se IsError == true
    public bool IsError { get; init; }

    // Quem disparou (pode ser UserId do sistema para eventos automáticos)
    public Guid TriggeredByUserId { get; init; }
    public string? TriggeredByUserName { get; init; } // Desnormalizado para leitura rápida

    // Timestamp imutável — nunca atualizado
    public DateTime OccurredAt { get; init; }
}

public enum OrderTimelineEventType
{
    // Ciclo de vida do pedido
    OrderCreated              = 0,
    PaymentRequested          = 1,
    PaymentConfirmed          = 2,
    PaymentFailed             = 3,

    // Documentação
    DocumentsRequested        = 10,
    DocumentsValidated        = 11,
    DocumentsRejected         = 12,

    // Videoconferência (para e-CPF/e-CNPJ A3)
    VideoConferenceScheduled  = 20,
    VideoConferenceCompleted  = 21,

    // Emissão na AC
    SubmittedToAc             = 30,
    ProtocolReceived          = 31,
    AcStatusUpdated           = 32,
    AcError                   = 33,
    CertificateIssued         = 34,

    // Administrativo
    OrderCancelled            = 40,
    OrderRefunded             = 41,
    PaymentLinkResent         = 42,
    SystemNote                = 50,
}
