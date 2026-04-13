using CoreAr.Crm.Domain.Exceptions;

namespace CoreAr.Crm.Domain.Entities;

/// <summary>
/// Raiz do Agregado de Pedido (Order Aggregate Root).
/// 
/// REGRA CENTRAL: O status só avança via TransitionTo().
/// Nenhum código externo pode escrever diretamente em Status.
/// A Timeline é append-only — nunca deletamos eventos.
/// </summary>
public class Order
{
    // ─── Identidade e Contexto ────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty; // "ORD-2025-00041"

    // ─── Multi-Tenant ─────────────────────────────────────────────────────────
    public Guid PaId { get; private set; }
    public string PaName { get; private set; } = string.Empty;
    public Guid ArId { get; private set; }
    public string ArName { get; private set; } = string.Empty;
    public Guid AgentUserId { get; private set; } // Quem abriu o pedido

    // ─── Produto e AC ─────────────────────────────────────────────────────────
    public string ProductCode { get; private set; } = string.Empty;  // "E-CPF-A3-1ANO"
    public string ProductName { get; private set; } = string.Empty;
    public CertificationType CertificationType { get; private set; } // A1, A3, Cloud
    public string AcProvider { get; private set; } = string.Empty;   // "SYNGULAR", "VALID"

    // ─── Cliente ─────────────────────────────────────────────────────────────
    public string CustomerName { get; private set; } = string.Empty;
    public string CustomerDocument { get; private set; } = string.Empty; // CPF/CNPJ
    public string CustomerEmail { get; private set; } = string.Empty;

    // ─── Financeiro (centavos) ────────────────────────────────────────────────
    public long TotalAmountInCents { get; private set; }
    public long NetAmountInCents { get; private set; }   // Após split

    // ─── Dados da AC (preenchidos após emissão) ───────────────────────────────
    public string? ProtocolTicket { get; private set; }
    public string? VideoConferenceUrl { get; private set; }
    public string? AcRawResponse { get; private set; }   // Para auditoria
    public DateTime? IssuedAt { get; private set; }

    // ─── Máquina de Estados ───────────────────────────────────────────────────
    public OrderStatus Status { get; private set; }

    // ─── Timeline (append-only, imutável) ────────────────────────────────────
    private readonly List<OrderTimelineEvent> _timeline = new();
    public IReadOnlyCollection<OrderTimelineEvent> Timeline => _timeline.AsReadOnly();

    // ─── Auditoria ────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // ─── Mapa de transições válidas (Máquina de Estados) ─────────────────────
    // Define quais transições são legítimas. Qualquer outra é rejeitada.
    private static readonly Dictionary<OrderStatus, HashSet<OrderStatus>> ValidTransitions = new()
    {
        [OrderStatus.Draft]           = [OrderStatus.PendingPayment, OrderStatus.Cancelled],
        [OrderStatus.PendingPayment]  = [OrderStatus.Paid, OrderStatus.Expired, OrderStatus.Cancelled],
        [OrderStatus.Paid]            = [OrderStatus.DocumentPendingValidation, OrderStatus.Refunded],
        [OrderStatus.DocumentPendingValidation] = [OrderStatus.DocumentValidated, OrderStatus.DocumentRejected],
        [OrderStatus.DocumentRejected]= [OrderStatus.DocumentPendingValidation, OrderStatus.Cancelled],
        [OrderStatus.DocumentValidated] = [OrderStatus.AwaitingVideoConference, OrderStatus.IssuingAtAc],
        [OrderStatus.AwaitingVideoConference] = [OrderStatus.VideoConferenceCompleted, OrderStatus.Cancelled],
        [OrderStatus.VideoConferenceCompleted] = [OrderStatus.IssuingAtAc],
        [OrderStatus.IssuingAtAc]     = [OrderStatus.Issued, OrderStatus.AcError],
        [OrderStatus.AcError]         = [OrderStatus.IssuingAtAc, OrderStatus.Cancelled],
        [OrderStatus.Issued]          = [],   // Estado terminal — sem transições
        [OrderStatus.Cancelled]       = [],
        [OrderStatus.Expired]         = [],
        [OrderStatus.Refunded]        = [],
    };

    // ─── Factory Method ───────────────────────────────────────────────────────
    public static Order Create(
        string productCode, string productName, CertificationType certType,
        string acProvider, long totalAmountInCents, long netAmountInCents,
        string customerName, string customerDocument, string customerEmail,
        Guid paId, string paName, Guid arId, string arName, Guid agentUserId)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = GenerateOrderNumber(),
            ProductCode = productCode,
            ProductName = productName,
            CertificationType = certType,
            AcProvider = acProvider,
            TotalAmountInCents = totalAmountInCents,
            NetAmountInCents = netAmountInCents,
            CustomerName = customerName,
            CustomerDocument = customerDocument,
            CustomerEmail = customerEmail,
            PaId = paId,
            PaName = paName,
            ArId = arId,
            ArName = arName,
            AgentUserId = agentUserId,
            Status = OrderStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        order.AppendTimelineEvent(
            OrderTimelineEventType.OrderCreated,
            "Pedido criado",
            $"Certificado {productName} para {customerName}",
            agentUserId);

        return order;
    }

    // ─── Transição de Status (o coração da Máquina de Estados) ───────────────
    public void TransitionTo(
        OrderStatus newStatus,
        string description,
        Guid triggeredByUserId,
        string? additionalData = null,
        bool isError = false)
    {
        if (!ValidTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOrderTransitionException(
                $"Transição inválida de '{Status}' para '{newStatus}'. " +
                $"Transições permitidas: [{string.Join(", ", allowed ?? [])}]");
        }

        var previousStatus = Status;
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        AppendTimelineEvent(
            MapStatusToEventType(newStatus),
            GetStatusDisplayName(newStatus),
            description,
            triggeredByUserId,
            additionalData,
            isError);
    }

    // ─── Métodos de Domínio Específicos ──────────────────────────────────────
    public void SetProtocolTicket(string ticket, string? rawResponse = null)
    {
        if (string.IsNullOrWhiteSpace(ticket))
            throw new DomainException("Número de protocolo não pode ser vazio.");

        ProtocolTicket = ticket;
        AcRawResponse = rawResponse;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetVideoConferenceUrl(string url)
    {
        VideoConferenceUrl = url;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsIssued(string protocolTicket)
    {
        SetProtocolTicket(protocolTicket);
        IssuedAt = DateTime.UtcNow;
    }

    public void AppendCustomEvent(
        string title, string description, Guid userId,
        string? data = null, bool isError = false)
    {
        AppendTimelineEvent(OrderTimelineEventType.SystemNote, title, description,
            userId, data, isError);
    }

    // ─── Privados ─────────────────────────────────────────────────────────────
    private void AppendTimelineEvent(
        OrderTimelineEventType type, string title, string description,
        Guid userId, string? additionalData = null, bool isError = false)
    {
        _timeline.Add(new OrderTimelineEvent
        {
            Id = Guid.NewGuid(),
            OrderId = Id,
            Type = type,
            Title = title,
            Description = description,
            AdditionalData = additionalData,
            IsError = isError,
            TriggeredByUserId = userId,
            OccurredAt = DateTime.UtcNow,
        });
    }

    private static string GenerateOrderNumber()
    {
        var year = DateTime.UtcNow.Year;
        var seq = Random.Shared.Next(10000, 99999);
        return $"ORD-{year}-{seq:D5}";
    }

    private static OrderTimelineEventType MapStatusToEventType(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment           => OrderTimelineEventType.PaymentRequested,
        OrderStatus.Paid                     => OrderTimelineEventType.PaymentConfirmed,
        OrderStatus.DocumentPendingValidation => OrderTimelineEventType.DocumentsRequested,
        OrderStatus.DocumentValidated        => OrderTimelineEventType.DocumentsValidated,
        OrderStatus.DocumentRejected         => OrderTimelineEventType.DocumentsRejected,
        OrderStatus.AwaitingVideoConference  => OrderTimelineEventType.VideoConferenceScheduled,
        OrderStatus.VideoConferenceCompleted => OrderTimelineEventType.VideoConferenceCompleted,
        OrderStatus.IssuingAtAc              => OrderTimelineEventType.SubmittedToAc,
        OrderStatus.Issued                   => OrderTimelineEventType.CertificateIssued,
        OrderStatus.AcError                  => OrderTimelineEventType.AcError,
        OrderStatus.Cancelled                => OrderTimelineEventType.OrderCancelled,
        OrderStatus.Refunded                 => OrderTimelineEventType.OrderRefunded,
        _                                    => OrderTimelineEventType.SystemNote,
    };

    private static string GetStatusDisplayName(OrderStatus status) => status switch
    {
        OrderStatus.PendingPayment           => "Aguardando Pagamento",
        OrderStatus.Paid                     => "Pagamento Confirmado",
        OrderStatus.DocumentPendingValidation => "Documentos Solicitados",
        OrderStatus.DocumentValidated        => "Documentos Validados",
        OrderStatus.DocumentRejected         => "Documentos Rejeitados",
        OrderStatus.AwaitingVideoConference  => "Aguardando Videoconferência",
        OrderStatus.VideoConferenceCompleted => "Videoconferência Concluída",
        OrderStatus.IssuingAtAc              => "Emitindo na AC",
        OrderStatus.Issued                   => "Certificado Emitido",
        OrderStatus.AcError                  => "Erro na AC",
        OrderStatus.Cancelled                => "Cancelado",
        OrderStatus.Refunded                 => "Estornado",
        _                                    => status.ToString(),
    };
}

// ─── Enums do Domínio ─────────────────────────────────────────────────────────
public enum OrderStatus
{
    Draft = 0,
    PendingPayment = 1,
    Paid = 2,
    DocumentPendingValidation = 3,
    DocumentValidated = 4,
    DocumentRejected = 5,
    AwaitingVideoConference = 6,
    VideoConferenceCompleted = 7,
    IssuingAtAc = 8,
    Issued = 9,
    AcError = 10,
    Cancelled = 11,
    Expired = 12,
    Refunded = 13,
}

public enum CertificationType { A1, A3, Cloud }
