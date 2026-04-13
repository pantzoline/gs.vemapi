namespace CoreAr.Ledger.Domain.Entities;

/// <summary>
/// Entrada imutável do Livro Razão (Partidas Dobradas).
/// REGRA DE OURO: Nunca atualize ou delete este registro.
/// Toda correção deve ser feita via lançamento de estorno (entrada reversa).
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; private init; }
    public Guid TenantId { get; private init; }
    public Guid PedidoId { get; private init; }
    public Guid UsuarioId { get; private init; }         // Parceiro, AGR ou AR que recebe/paga
    public LedgerEntryType Tipo { get; private init; }   // Credito ou Debito
    public decimal Valor { get; private init; }          // Sempre positivo; tipo define a direção
    public string Descricao { get; private init; }
    public Guid ReferenciaId { get; private init; }      // Agrupa todas as entradas de um split batch
    public DateTimeOffset CriadoEm { get; private init; }

    private LedgerEntry() { } // Para hidratação pelo ORM

    /// <summary>
    /// Factory method — único ponto de criação de uma entrada no Ledger.
    /// </summary>
    public static LedgerEntry Criar(
        Guid tenantId,
        Guid pedidoId,
        Guid usuarioId,
        LedgerEntryType tipo,
        decimal valor,
        string descricao,
        Guid referenciaId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        if (pedidoId == Guid.Empty) throw new ArgumentException("PedidoId é obrigatório.", nameof(pedidoId));
        if (usuarioId == Guid.Empty) throw new ArgumentException("UsuarioId é obrigatório.", nameof(usuarioId));
        if (valor <= 0) throw new ArgumentException("Valor de entrada no Ledger deve ser positivo.", nameof(valor));
        if (string.IsNullOrWhiteSpace(descricao)) throw new ArgumentException("Descrição é obrigatória.", nameof(descricao));
        if (referenciaId == Guid.Empty) throw new ArgumentException("ReferenciaId é obrigatória para rastreabilidade.", nameof(referenciaId));

        return new LedgerEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PedidoId = pedidoId,
            UsuarioId = usuarioId,
            Tipo = tipo,
            Valor = valor,
            Descricao = descricao,
            ReferenciaId = referenciaId,
            CriadoEm = DateTimeOffset.UtcNow
        };
    }
}

public enum LedgerEntryType
{
    Credito,
    Debito
}
