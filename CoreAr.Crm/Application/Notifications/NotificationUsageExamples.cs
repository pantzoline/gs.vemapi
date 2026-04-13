// Como integrar NotificationService nos serviços existentes
//
// ─── Exemplo 1: No WebhookController (Asaas/Iugu) ────────────────────────────
// Após confirmar pagamento, dispara notificação ao Tenant

/*
[HttpPost("asaas")]
public async Task<IActionResult> HandleAsaasWebhook([FromBody] AsaasWebhookPayload payload)
{
    if (payload.Event == "PAYMENT_RECEIVED")
    {
        var order = await _paymentService.ConfirmPaymentAsync(payload.PaymentId);

        // 1. Notifica o Tenant (PA/AR) sobre o pagamento
        await _notificationService.NotifyPaymentConfirmedAsync(
            tenantId:    order.PaId.ToString(),
            orderNumber: order.OrderNumber,
            amountReais: order.TotalAmountInCents / 100m,
            orderId:     order.Id.ToString()
        );

        // 2. Publica evento para emissão na AC (via RabbitMQ)
        _messageBus.Publish(new OrderPaidIntegrationEvent(order.Id));
    }
    return Ok();
}
*/

// ─── Exemplo 2: No OrderService (após emissão na AC) ─────────────────────────

/*
public async Task ProcessAcIssuanceResultAsync(Guid orderId, string protocolTicket)
{
    var order = await _db.Orders.FindAsync(orderId);
    order.MarkAsIssued(protocolTicket);
    order.TransitionTo(OrderStatus.Issued, "Emitido pela AC", Guid.Empty);
    await _db.SaveChangesAsync();

    // Notificação vai apenas para o Tenant dono do pedido (PA/AR)
    await _notificationService.NotifyCertificateIssuedAsync(
        tenantId:       order.PaId.ToString(),
        customerName:   order.CustomerName,
        protocolTicket: protocolTicket,
        orderId:        orderId.ToString()
    );
}
*/

// ─── Exemplo 3: Em um BackgroundService (polling da AC) ──────────────────────

/*
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    await foreach (var order in GetOrdersPendingAcCheckAsync(stoppingToken))
    {
        var acStatus = await _acGateway.CheckStatusAsync(order.ProtocolTicket, stoppingToken);

        if (acStatus.StatusCode == "ISSUED")
        {
            await _orderService.TransitionStatusAsync(order.Id, OrderStatus.Issued, "Emitido");

            await _notificationService.NotifyCertificateIssuedAsync(
                order.PaId.ToString(),
                order.CustomerName,
                order.ProtocolTicket!,
                order.Id.ToString(),
                stoppingToken
            );
        }
        else if (acStatus.StatusCode == "ERROR")
        {
            await _notificationService.NotifyAcErrorAsync(
                userId:       order.AgentUserId.ToString(),
                tenantId:     order.PaId.ToString(),
                orderNumber:  order.OrderNumber,
                acProvider:   order.AcProvider,
                errorMessage: acStatus.ErrorMessage ?? "Erro desconhecido",
                orderId:      order.Id.ToString(),
                ct:           stoppingToken
            );
        }
    }
}
*/

// ─── Exemplo 4: No LedgerService (transação suspeita acima do limite) ─────────

/*
private async Task CheckTransactionLimitsAsync(Transaction tx, string arName)
{
    const decimal SUSPICIOUS_THRESHOLD_REAIS = 50_000m;
    var amountReais = tx.Entries.Where(e => e.EntryType == EntryType.Debit)
                                .Sum(e => e.AmountInCents) / 100m;

    if (amountReais > SUSPICIOUS_THRESHOLD_REAIS)
    {
        await _notificationService.NotifySuspiciousTransactionAsync(
            masterTenantId: "master",
            arName:         arName,
            amount:         amountReais,
            transactionId:  tx.Id.ToString()
        );
    }
}
*/

// Arquivo de referência — não compilado diretamente
// Serve como documentação de uso do NotificationService no projeto
