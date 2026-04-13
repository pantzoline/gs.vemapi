using System.Security.Cryptography;
using System.Text;
using CoreAr.Ledger.Domain.Events;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CoreAr.Checkout.Api.Controllers;

/// <summary>
/// Listener de Webhooks — Gateway de Pagamento (ex: Pagar.me / Stripe).
///
/// SEGURANÇA:
///   - Valida assinatura HMAC-SHA256 antes de processar qualquer payload.
///   - Implementa idempotência via Redis (middleware injeta antes deste controller).
///   - Toda mudança de status roda em transaction ACID (via serviço de domínio).
/// </summary>
[ApiController]
[Route("api/webhooks/gateway")]
public sealed class WebhookGatewayController : ControllerBase
{
    private readonly IWebhookSignatureValidator _signatureValidator;
    private readonly IOrderEventPublisher _eventPublisher;
    private readonly ILogger<WebhookGatewayController> _logger;

    public WebhookGatewayController(
        IWebhookSignatureValidator signatureValidator,
        IOrderEventPublisher eventPublisher,
        ILogger<WebhookGatewayController> logger)
    {
        _signatureValidator = signatureValidator;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/webhooks/gateway
    /// Recebe notificações do gateway de pagamento.
    /// O header X-Idempotency-Key deve ser enviado pelo chamador (gateway).
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> ReceberWebhook(
        [FromBody] GatewayWebhookPayload payload,
        [FromHeader(Name = "X-Gateway-Signature")] string assinatura,
        [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        // 1. Validação da assinatura antes de qualquer processamento
        var payloadBruto = await ObterBodyBrutoAsync();
        if (!await _signatureValidator.ValidarAsync(payloadBruto, assinatura, payload.TenantId))
        {
            _logger.LogWarning(
                "ALERTA DE SEGURANÇA: Assinatura inválida recebida no webhook. " +
                "TenantId={TenantId}, TransactionId={TransactionId}",
                payload.TenantId, payload.TransactionId);

            // Retorna 200 mesmo em caso de falha para não revelar detalhes ao atacante.
            // Internamente já logamos e descartamos.
            return Ok(new { mensagem = "Recebido." });
        }

        // 2. Processar apenas eventos relevantes
        if (payload.Evento == "transaction.paid")
        {
            var evento = new OrderPaidEvent(
                TenantId: payload.TenantId,
                PedidoId: payload.PedidoId,
                ValorTotal: payload.ValorEmCentavos / 100m, // Centavos → Decimal com 4 casas
                GatewayTransactionId: payload.TransactionId,
                OcorridoEm: DateTimeOffset.UtcNow
            );

            await _eventPublisher.PublicarAsync(evento, cancellationToken);

            _logger.LogInformation(
                "OrderPaidEvent publicado com sucesso. PedidoId={PedidoId}, Valor={Valor}",
                evento.PedidoId, evento.ValorTotal);
        }

        return Ok(new { mensagem = "Recebido." });
    }

    // O body bruto é necessário para validação HMAC antes da desserialização
    private async Task<string> ObterBodyBrutoAsync()
    {
        Request.Body.Seek(0, System.IO.SeekOrigin.Begin);
        using var reader = new System.IO.StreamReader(Request.Body);
        return await reader.ReadToEndAsync();
    }
}

// ===[ DTOs ]===
public sealed record GatewayWebhookPayload(
    Guid TenantId,
    Guid PedidoId,
    string Evento,              // ex: "transaction.paid", "transaction.refunded"
    string TransactionId,
    long ValorEmCentavos        // Gateway envia em centavos (long). Convertemos para DECIMAL no domínio.
);

// ===[ Interfaces de abstração ]===
public interface IWebhookSignatureValidator
{
    Task<bool> ValidarAsync(string payloadBruto, string assinatura, Guid tenantId);
}

public interface IOrderEventPublisher
{
    Task PublicarAsync(OrderPaidEvent evento, CancellationToken cancellationToken);
}
