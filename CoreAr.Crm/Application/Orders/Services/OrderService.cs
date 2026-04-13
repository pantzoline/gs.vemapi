using CoreAr.Crm.Domain.Entities;
using CoreAr.Crm.Domain.Interfaces.Gateways;
using CoreAr.Identity.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoreAr.Crm.Application.Orders.Services;

public interface IOrderService
{
    Task<Order> GetByIdAsync(Guid orderId, CancellationToken ct = default);
    Task<Order> GetByIdWithAcStatusAsync(Guid orderId, CancellationToken ct = default);
    Task<OrderListResult> GetPagedAsync(OrderListQuery query, CancellationToken ct = default);
    Task TransitionStatusAsync(Guid orderId, OrderStatus newStatus, string description, string? additionalData = null, CancellationToken ct = default);
    Task ResendPaymentLinkAsync(Guid orderId, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly CrmDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcGatewayFactory _acGatewayFactory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        CrmDbContext db,
        ICurrentUserService currentUser,
        IAcGatewayFactory acGatewayFactory,
        ILogger<OrderService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _acGatewayFactory = acGatewayFactory;
        _logger = logger;
    }

    // ─── Detalhes do pedido (sem consulta à AC) ───────────────────────────────
    public async Task<Order> GetByIdAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Timeline.OrderByDescending(e => e.OccurredAt))
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException($"Pedido {orderId} não encontrado.");

        return order;
    }

    // ─── Detalhes com status em tempo real da AC ──────────────────────────────
    public async Task<Order> GetByIdWithAcStatusAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await GetByIdAsync(orderId, ct);

        // Só consulta a AC se o pedido tiver protocolo e estiver em status ativo
        if (!string.IsNullOrEmpty(order.ProtocolTicket) &&
            order.Status is not (OrderStatus.Issued or OrderStatus.Cancelled or OrderStatus.Refunded))
        {
            try
            {
                var gateway = _acGatewayFactory.GetGateway(order.AcProvider);
                var acStatus = await gateway.CheckStatusAsync(order.ProtocolTicket, ct);

                // Sincroniza automaticamente se a AC mudou o status
                await SyncAcStatusAsync(order, acStatus, ct);
            }
            catch (Exception ex)
            {
                // Log mas NÃO falha — o status local é o fallback
                _logger.LogWarning(ex,
                    "Falha ao consultar status em tempo real na AC {Provider} para pedido {OrderId}",
                    order.AcProvider, orderId);
            }
        }

        return await GetByIdAsync(orderId, ct); // Recarrega com possíveis atualizações
    }

    // ─── Listagem paginada com filtros (server-side) ──────────────────────────
    public async Task<OrderListResult> GetPagedAsync(OrderListQuery query, CancellationToken ct = default)
    {
        var q = _db.Orders.AsNoTracking(); // Global Query Filter já aplica TenantId

        // ─── Filtros de Faceta ────────────────────────────────────────────────
        if (query.Statuses?.Any() == true)
            q = q.Where(o => query.Statuses.Contains(o.Status));

        if (query.AcProviders?.Any() == true)
            q = q.Where(o => query.AcProviders.Contains(o.AcProvider));

        if (query.CertificationTypes?.Any() == true)
            q = q.Where(o => query.CertificationTypes.Contains(o.CertificationType));

        if (query.PaIds?.Any() == true)
            q = q.Where(o => query.PaIds.Contains(o.PaId));

        if (query.From.HasValue)
            q = q.Where(o => o.CreatedAt >= query.From.Value);

        if (query.To.HasValue)
            q = q.Where(o => o.CreatedAt <= query.To.Value);

        // ─── Busca Global (CPF, Nome ou Protocolo) ────────────────────────────
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim().ToLower();
            q = q.Where(o =>
                o.CustomerDocument.Contains(searchTerm) ||
                o.CustomerName.ToLower().Contains(searchTerm) ||
                (o.ProtocolTicket != null && o.ProtocolTicket.Contains(searchTerm)) ||
                o.OrderNumber.Contains(searchTerm));
        }

        // ─── Contagem total (para paginação do frontend) ─────────────────────
        var totalCount = await q.CountAsync(ct);

        // ─── Sorting dinâmico ─────────────────────────────────────────────────
        q = (query.SortBy, query.SortDescending) switch
        {
            ("customerName", true)  => q.OrderByDescending(o => o.CustomerName),
            ("customerName", false) => q.OrderBy(o => o.CustomerName),
            ("totalAmount", true)   => q.OrderByDescending(o => o.TotalAmountInCents),
            ("totalAmount", false)  => q.OrderBy(o => o.TotalAmountInCents),
            ("status", true)        => q.OrderByDescending(o => o.Status),
            ("status", false)       => q.OrderBy(o => o.Status),
            _                       => q.OrderByDescending(o => o.CreatedAt), // Default
        };

        // ─── Projeção: só os campos necessários para a tabela ─────────────────
        var items = await q
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new OrderSummaryDto
            {
                Id               = o.Id,
                OrderNumber      = o.OrderNumber,
                CustomerName     = o.CustomerName,
                CustomerDocument = o.CustomerDocument,
                ProductName      = o.ProductName,
                AcProvider       = o.AcProvider,
                CertificationType = o.CertificationType.ToString(),
                Status           = o.Status,
                StatusLabel      = GetStatusLabel(o.Status),
                TotalAmountInCents = o.TotalAmountInCents,
                ProtocolTicket   = o.ProtocolTicket,
                PaName           = o.PaName,
                CreatedAt        = o.CreatedAt,
            })
            .ToListAsync(ct);

        return new OrderListResult(items, totalCount, query.Page, query.PageSize);
    }

    // ─── Transição de Status via API ─────────────────────────────────────────
    public async Task TransitionStatusAsync(
        Guid orderId, OrderStatus newStatus, string description,
        string? additionalData = null, CancellationToken ct = default)
    {
        // Carrega para rastreamento (sem AsNoTracking aqui — precisamos salvar)
        var order = await _db.Orders
            .Include(o => o.Timeline)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException($"Pedido {orderId} não encontrado.");

        order.TransitionTo(newStatus, description, _currentUser.UserId, additionalData);

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Pedido {OrderNumber} transicionou para {Status}. UserId={UserId}",
            order.OrderNumber, newStatus, _currentUser.UserId);
    }

    // ─── Reenvio do Link de Pagamento ────────────────────────────────────────
    public async Task ResendPaymentLinkAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Timeline)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException($"Pedido {orderId} não encontrado.");

        if (order.Status != OrderStatus.PendingPayment)
            throw new DomainOperationException("Só é possível reenviar link para pedidos aguardando pagamento.");

        // Append do evento na timeline sem mudar o status
        order.AppendCustomEvent(
            "Link de Pagamento Reenviado",
            $"Link de pagamento reenviado para {order.CustomerEmail}",
            _currentUser.UserId);

        await _db.SaveChangesAsync(ct);

        // TODO: Disparar evento de integração para o serviço de email/SMS
    }

    // ─── Sincronização com status da AC ──────────────────────────────────────
    private async Task SyncAcStatusAsync(Order order, AcOrderStatusResponse acStatus, CancellationToken ct)
    {
        if (acStatus.StatusCode == "AWAITING_VIDEO_CONFERENCE" &&
            order.Status != OrderStatus.AwaitingVideoConference)
        {
            var trackable = await _db.Orders.Include(o => o.Timeline)
                .FirstAsync(o => o.Id == order.Id, ct);

            if (!string.IsNullOrEmpty(acStatus.VideoConferenceUrl))
                trackable.SetVideoConferenceUrl(acStatus.VideoConferenceUrl);

            trackable.TransitionTo(
                OrderStatus.AwaitingVideoConference,
                $"Status atualizado automaticamente pela AC {order.AcProvider}",
                Guid.Empty); // Guid.Empty = sistema

            await _db.SaveChangesAsync(ct);
        }
    }

    private static string GetStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Draft                    => "Rascunho",
        OrderStatus.PendingPayment           => "Aguard. Pagamento",
        OrderStatus.Paid                     => "Pago",
        OrderStatus.DocumentPendingValidation => "Doc. Pendentes",
        OrderStatus.DocumentValidated        => "Doc. Validados",
        OrderStatus.DocumentRejected         => "Doc. Rejeitados",
        OrderStatus.AwaitingVideoConference  => "Aguard. Vídeo",
        OrderStatus.VideoConferenceCompleted => "Vídeo OK",
        OrderStatus.IssuingAtAc              => "Emitindo na AC",
        OrderStatus.Issued                   => "Emitido",
        OrderStatus.AcError                  => "Erro AC",
        OrderStatus.Cancelled                => "Cancelado",
        OrderStatus.Expired                  => "Expirado",
        OrderStatus.Refunded                 => "Estornado",
        _                                    => status.ToString()
    };
}

// ─── Tipos de suporte ─────────────────────────────────────────────────────────
public record OrderListQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Search { get; init; }
    public string? SortBy { get; init; }
    public bool SortDescending { get; init; } = true;
    public List<OrderStatus>? Statuses { get; init; }
    public List<string>? AcProviders { get; init; }
    public List<CertificationType>? CertificationTypes { get; init; }
    public List<Guid>? PaIds { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
}

public record OrderListResult(
    IReadOnlyList<OrderSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPrevPage => Page > 1;
}

public record OrderSummaryDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public string CustomerDocument { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string AcProvider { get; init; } = string.Empty;
    public string CertificationType { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public string StatusLabel { get; init; } = string.Empty;
    public long TotalAmountInCents { get; init; }
    public string? ProtocolTicket { get; init; }
    public string PaName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Stubs que serão substituídos pelos tipos reais
public class AcOrderStatusResponse { public string StatusCode { get; set; } = ""; public string? VideoConferenceUrl { get; set; } }
public interface IAcGatewayFactory { ICertificationAuthorityGateway GetGateway(string provider); }
public interface ICertificationAuthorityGateway { Task<AcOrderStatusResponse> CheckStatusAsync(string ticket, CancellationToken ct); }
public class NotFoundException(string msg) : Exception(msg);
public class DomainOperationException(string msg) : Exception(msg);
