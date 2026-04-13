using CoreAr.Ledger.Domain.Entities;

namespace CoreAr.Ledger.Domain.Services;

/// <summary>
/// Motor de Split de Comissionamento.
/// 
/// REGRA DE NEGÓCIO CRÍTICA:
///   1. A soma dos percentuais do contrato DEVE ser exatamente 100% (1.0).
///   2. A soma de todos os créditos gerados DEVE ser igual ao valor total.
///   3. Para conciliação: um Débito de 'valorTotal' na conta-mestre é emitido,
///      seguido de Créditos para cada nível. A soma líquida = ZERO.
/// 
/// ARREDONDAMENTO:
///   Usamos Math.Round com MidpointRounding.AwayFromZero (padrão bancário).
///   O "penny" restante vai sempre para a AR (último crédito calculado por diferença).
/// </summary>
public sealed class SplitComissionamentoService
{
    private const decimal TOTAL_ESPERADO = 1.0m;
    private const int CASAS_DECIMAIS = 4;

    /// <summary>
    /// Calcula e retorna as entradas do Ledger para um split de pagamento.
    /// Lança exceção se o contrato for inválido ou o split não fechar em zero.
    /// </summary>
    public IReadOnlyList<LedgerEntry> CalcularSplit(
        ContratoComissionamento contrato,
        Guid pedidoId,
        decimal valorTotal)
    {
        ValidarContrato(contrato);

        if (valorTotal <= 0)
            throw new ArgumentException("Valor total do pedido deve ser positivo.", nameof(valorTotal));

        var referenciaId = Guid.NewGuid(); // Agrupa todas as entradas deste split
        var entradas = new List<LedgerEntry>();

        // --- DÉBITO MESTRE: representa o valor saindo do caixa do pedido ---
        entradas.Add(LedgerEntry.Criar(
            tenantId: contrato.TenantId,
            pedidoId: pedidoId,
            usuarioId: contrato.UsuarioArId,
            tipo: LedgerEntryType.Debito,
            valor: valorTotal,
            descricao: $"Débito total do pedido {pedidoId} para distribuição de split",
            referenciaId: referenciaId
        ));

        // --- CRÉDITO: Parceiro ---
        var valorParceiro = Arredondar(valorTotal * contrato.PercentualParceiro);
        entradas.Add(LedgerEntry.Criar(
            tenantId: contrato.TenantId,
            pedidoId: pedidoId,
            usuarioId: contrato.UsuarioParceitoId,
            tipo: LedgerEntryType.Credito,
            valor: valorParceiro,
            descricao: $"Comissão Parceiro ({contrato.PercentualParceiro:P0}) - Pedido {pedidoId}",
            referenciaId: referenciaId
        ));

        // --- CRÉDITO: AGR ---
        var valorAgr = Arredondar(valorTotal * contrato.PercentualAgr);
        entradas.Add(LedgerEntry.Criar(
            tenantId: contrato.TenantId,
            pedidoId: pedidoId,
            usuarioId: contrato.UsuarioAgrId,
            tipo: LedgerEntryType.Credito,
            valor: valorAgr,
            descricao: $"Comissão AGR ({contrato.PercentualAgr:P0}) - Pedido {pedidoId}",
            referenciaId: referenciaId
        ));

        // --- CRÉDITO: AR (recebe a diferença para absorver arredondamento) ---
        // Técnica: "penny goes to the house"
        var valorAr = valorTotal - valorParceiro - valorAgr;
        if (valorAr <= 0)
            throw new InvalidOperationException(
                $"Erro de arredondamento: o split deixou valor inválido para a AR ({valorAr}). " +
                "Verifique os percentuais do contrato.");

        entradas.Add(LedgerEntry.Criar(
            tenantId: contrato.TenantId,
            pedidoId: pedidoId,
            usuarioId: contrato.UsuarioArId,
            tipo: LedgerEntryType.Credito,
            valor: valorAr,
            descricao: $"Comissão AR ({contrato.PercentualAr:P0}) - Pedido {pedidoId}",
            referenciaId: referenciaId
        ));

        // --- VERIFICAÇÃO DE CONCILIAÇÃO: soma líquida DEVE ser ZERO ---
        AssertConciliacao(entradas, valorTotal);

        return entradas.AsReadOnly();
    }

    // =========================================================================
    // Métodos privados de validação e cálculo
    // =========================================================================

    private static void ValidarContrato(ContratoComissionamento contrato)
    {
        ArgumentNullException.ThrowIfNull(contrato);

        var somaPercentuais = contrato.PercentualAr
                            + contrato.PercentualAgr
                            + contrato.PercentualParceiro;

        // Tolerância de 0.0001 para lidar com aritimética decimal
        if (Math.Abs(somaPercentuais - TOTAL_ESPERADO) > 0.0001m)
            throw new ArgumentException(
                $"CONTRATO INVÁLIDO: a soma dos percentuais é {somaPercentuais:P4}, " +
                $"mas deve ser exatamente 100%. Verifique o contrato do tenant {contrato.TenantId}.");

        if (contrato.PercentualAr < 0 || contrato.PercentualAgr < 0 || contrato.PercentualParceiro < 0)
            throw new ArgumentException("Percentuais de comissão não podem ser negativos.");
    }

    private static void AssertConciliacao(List<LedgerEntry> entradas, decimal valorTotal)
    {
        var totalCreditos = entradas
            .Where(e => e.Tipo == LedgerEntryType.Credito)
            .Sum(e => e.Valor);

        var totalDebitos = entradas
            .Where(e => e.Tipo == LedgerEntryType.Debito)
            .Sum(e => e.Valor);

        var liquidoFinal = totalDebitos - totalCreditos;

        if (liquidoFinal != 0m)
            throw new InvalidOperationException(
                $"FALHA DE CONCILIAÇÃO: o Ledger não fechou em zero. " +
                $"Débitos={totalDebitos:C}, Créditos={totalCreditos:C}, Diferença={liquidoFinal:C}. " +
                $"Valor total do pedido={valorTotal:C}.");
    }

    private static decimal Arredondar(decimal valor) =>
        Math.Round(valor, CASAS_DECIMAIS, MidpointRounding.AwayFromZero);
}
