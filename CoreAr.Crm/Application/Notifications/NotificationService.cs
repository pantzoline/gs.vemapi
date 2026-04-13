using CoreAr.Crm.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CoreAr.Crm.Application.Notifications;

// ─── Modelo de Notificação ────────────────────────────────────────────────────

public enum NotificationType
{
    Success      = 0,
    Info         = 1,
    Warning      = 2,
    CriticalError = 3,
    Financial    = 4,
}

/// <summary>
/// Payload enviado ao cliente via SignalR.
/// Deve ser leve — apenas o essencial para o toast e o sininho.
/// </summary>
public record NotificationPayload
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ActionUrl { get; init; }      // Link "Ver pedido"
    public string? ActionLabel { get; init; }    // Texto do link
    public string? RelatedEntityId { get; init; } // OrderId ou TransactionId
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
}

// ─── Interface do Serviço ─────────────────────────────────────────────────────

public interface INotificationService
{
    // Envia para um usuário específico (por UserId)
    Task SendToUserAsync(string userId, NotificationPayload notification, CancellationToken ct = default);

    // Envia para TODOS os usuários de um Tenant (AR ou PA inteiro)
    Task SendToTenantAsync(string tenantId, NotificationPayload notification, CancellationToken ct = default);

    // Envia apenas para usuários com determinada Role (ex: todos os Masters)
    Task SendToRoleAsync(string role, NotificationPayload notification, CancellationToken ct = default);

    // Broadcast global (apenas para Masters e sistemas de alertas críticos)
    Task BroadcastAsync(NotificationPayload notification, CancellationToken ct = default);

    // ─── Factory methods de domínio para uso semântico ──────────────────────

    Task NotifyPaymentConfirmedAsync(
        string tenantId, string orderNumber, decimal amountReais,
        string orderId, CancellationToken ct = default);

    Task NotifyCertificateIssuedAsync(
        string tenantId, string customerName, string protocolTicket,
        string orderId, CancellationToken ct = default);

    Task NotifyDocumentRejectedAsync(
        string userId, string orderNumber, string reason,
        string orderId, CancellationToken ct = default);

    Task NotifyAcErrorAsync(
        string userId, string tenantId, string orderNumber,
        string acProvider, string errorMessage,
        string orderId, CancellationToken ct = default);

    Task NotifySuspiciousTransactionAsync(
        string masterTenantId, string arName, decimal amount,
        string transactionId, CancellationToken ct = default);
}

// ─── Implementação ────────────────────────────────────────────────────────────

/// <summary>
/// Serviço injetável que abstrai o envio de notificações via SignalR.
/// Pode ser chamado de qualquer Service, Controller ou Worker sem acoplamento
/// direto ao Hub ou à camada de transporte.
///
/// Uso em qualquer ponto do sistema:
///   await _notificationService.NotifyPaymentConfirmedAsync(tenantId, ...);
/// </summary>
public class NotificationService : INotificationService
{
    // IHubContext é thread-safe e pode ser injetado em qualquer lugar
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    // Nome do método invocado no cliente (case-sensitive deve bater com o frontend)
    private const string CLIENT_METHOD = "ReceiveNotification";

    public NotificationService(
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    // ─── Primitivas de envio ──────────────────────────────────────────────────

    public async Task SendToUserAsync(
        string userId, NotificationPayload notification, CancellationToken ct = default)
    {
        await SafeSendAsync(
            _hub.Clients.Group($"User_{userId}"),
            notification, $"User_{userId}", ct);
    }

    public async Task SendToTenantAsync(
        string tenantId, NotificationPayload notification, CancellationToken ct = default)
    {
        await SafeSendAsync(
            _hub.Clients.Group($"Tenant_{tenantId}"),
            notification, $"Tenant_{tenantId}", ct);
    }

    public async Task SendToRoleAsync(
        string role, NotificationPayload notification, CancellationToken ct = default)
    {
        await SafeSendAsync(
            _hub.Clients.Group($"Role_{role}"),
            notification, $"Role_{role}", ct);
    }

    public async Task BroadcastAsync(
        NotificationPayload notification, CancellationToken ct = default)
    {
        await SafeSendAsync(_hub.Clients.All, notification, "ALL", ct);
    }

    // ─── Factory Methods de Domínio (semânticos e reutilizáveis) ─────────────

    public Task NotifyPaymentConfirmedAsync(
        string tenantId, string orderNumber, decimal amountReais,
        string orderId, CancellationToken ct = default)
        => SendToTenantAsync(tenantId, new NotificationPayload
        {
            Type         = NotificationType.Financial,
            Title        = "💰 Pagamento Confirmado",
            Message      = $"Pedido {orderNumber} — R$ {amountReais:N2} recebido via PIX",
            ActionUrl    = $"/dashboard/orders/{orderId}",
            ActionLabel  = "Ver Pedido",
            RelatedEntityId = orderId,
        }, ct);

    public Task NotifyCertificateIssuedAsync(
        string tenantId, string customerName, string protocolTicket,
        string orderId, CancellationToken ct = default)
        => SendToTenantAsync(tenantId, new NotificationPayload
        {
            Type         = NotificationType.Success,
            Title        = "🎉 Certificado Emitido",
            Message      = $"{customerName} — Protocolo: {protocolTicket}",
            ActionUrl    = $"/dashboard/orders/{orderId}",
            ActionLabel  = "Ver Protocolo",
            RelatedEntityId = orderId,
        }, ct);

    public Task NotifyDocumentRejectedAsync(
        string userId, string orderNumber, string reason,
        string orderId, CancellationToken ct = default)
        => SendToUserAsync(userId, new NotificationPayload
        {
            Type         = NotificationType.Warning,
            Title        = "⚠️ Documento Rejeitado",
            Message      = $"Pedido {orderNumber}: {reason}",
            ActionUrl    = $"/dashboard/orders/{orderId}",
            ActionLabel  = "Corrigir Documentos",
            RelatedEntityId = orderId,
        }, ct);

    public async Task NotifyAcErrorAsync(
        string userId, string tenantId, string orderNumber,
        string acProvider, string errorMessage,
        string orderId, CancellationToken ct = default)
    {
        var payload = new NotificationPayload
        {
            Type = NotificationType.CriticalError,
            Title = $"🔴 Erro na AC — {acProvider}",
            Message = $"Pedido {orderNumber}: {errorMessage}",
            ActionUrl = $"/dashboard/orders/{orderId}",
            ActionLabel = "Investigar",
            RelatedEntityId = orderId,
        };

        // Notifica tanto o agente quanto a equipe de suporte (Role Master)
        await Task.WhenAll(
            SendToUserAsync(userId, payload, ct),
            SendToRoleAsync("ROLE_MASTER", payload with
            {
                Title = $"🔴 Erro AC (Alerta Master) — {acProvider}",
                Message = $"Pedido {orderNumber} falhou na {acProvider}: {errorMessage}"
            }, ct)
        );
    }

    public Task NotifySuspiciousTransactionAsync(
        string masterTenantId, string arName, decimal amount,
        string transactionId, CancellationToken ct = default)
        => SendToRoleAsync("ROLE_MASTER", new NotificationPayload
        {
            Type         = NotificationType.CriticalError,
            Title        = "🚨 Transação Suspeita Detectada",
            Message      = $"AR: {arName} — Valor: R$ {amount:N2} acima do limite configurado",
            ActionUrl    = $"/dashboard/ledger/{transactionId}",
            ActionLabel  = "Investigar Agora",
            RelatedEntityId = transactionId,
        }, ct);

    // ─── Helper de envio seguro (não deixa exceção de network vazar) ──────────
    private async Task SafeSendAsync(
        IClientProxy clients, NotificationPayload payload,
        string target, CancellationToken ct)
    {
        try
        {
            await clients.SendAsync(CLIENT_METHOD, payload, ct);
            _logger.LogDebug(
                "Notificação enviada → {Target} | Tipo: {Type} | Id: {Id}",
                target, payload.Type, payload.Id);
        }
        catch (Exception ex)
        {
            // Uma falha de notificação NUNCA deve cancelar a operação de negócio
            _logger.LogError(ex,
                "Falha ao enviar notificação SignalR para {Target}. Operação principal não afetada.",
                target);
        }
    }
}
