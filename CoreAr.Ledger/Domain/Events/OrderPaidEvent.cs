namespace CoreAr.Ledger.Domain.Events;

/// <summary>
/// Evento de domínio disparado quando um pagamento é confirmado pelo gateway.
/// Consumido pelo Motor de Split para gerar as entradas no Ledger.
/// </summary>
public sealed record OrderPaidEvent(
    Guid TenantId,
    Guid PedidoId,
    decimal ValorTotal,           // NUMERIC: sem float, sem double
    string GatewayTransactionId,
    DateTimeOffset OcorridoEm
);
