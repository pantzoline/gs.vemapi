namespace CoreAr.Ledger.Domain.Entities;

/// <summary>
/// Representa o contrato de comissionamento de uma AR,
/// definindo as fatias de cada nível da hierarquia.
/// A soma de todos os percentuais DEVE ser exatamente 100%.
/// </summary>
public sealed class ContratoComissionamento
{
    public Guid TenantId { get; init; }       // AR dona do contrato
    public Guid UsuarioArId { get; init; }
    public Guid UsuarioAgrId { get; init; }
    public Guid UsuarioParceitoId { get; init; }

    /// <summary>Percentual para a AR (ex: 0.70 = 70%)</summary>
    public decimal PercentualAr { get; init; }

    /// <summary>Percentual para o AGR (ex: 0.10 = 10%)</summary>
    public decimal PercentualAgr { get; init; }

    /// <summary>Percentual para o Parceiro (ex: 0.20 = 20%)</summary>
    public decimal PercentualParceiro { get; init; }
}
