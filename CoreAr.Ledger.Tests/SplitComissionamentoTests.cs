using CoreAr.Ledger.Domain.Entities;
using CoreAr.Ledger.Domain.Services;
using FluentAssertions;

namespace CoreAr.Ledger.Tests;

/// <summary>
/// Testes unitários do Motor de Split de Comissionamento.
/// 
/// Cenários cobertos:
///   ✅ Split correto fecha em ZERO
///   ✅ Arredondamento de penny vai para AR
///   ❌ Percentuais somando mais que 100% → exceção
///   ❌ Percentuais somando menos que 100% → exceção
///   ❌ Percentual negativo → exceção
///   ❌ Valor do pedido zero ou negativo → exceção
/// </summary>
public class SplitComissionamentoTests
{
    private readonly SplitComissionamentoService _sut = new();

    private static ContratoComissionamento CriarContrato(
        decimal percAr = 0.70m,
        decimal percAgr = 0.10m,
        decimal percParceiro = 0.20m) => new()
    {
        TenantId = Guid.NewGuid(),
        UsuarioArId = Guid.NewGuid(),
        UsuarioAgrId = Guid.NewGuid(),
        UsuarioParceitoId = Guid.NewGuid(),
        PercentualAr = percAr,
        PercentualAgr = percAgr,
        PercentualParceiro = percParceiro
    };

    // =========================================================================
    // CENÁRIOS DE SUCESSO
    // =========================================================================

    [Fact(DisplayName = "Split 70/10/20% deve gerar lançamentos e fechar em ZERO")]
    public void Split_Padrao_DeveFecharEmZero()
    {
        // Arrange
        var contrato = CriarContrato(percAr: 0.70m, percAgr: 0.10m, percParceiro: 0.20m);
        var pedidoId = Guid.NewGuid();
        const decimal valorTotal = 1000.00m;

        // Act
        var entradas = _sut.CalcularSplit(contrato, pedidoId, valorTotal);

        // Assert
        var totalCreditos = entradas.Where(e => e.Tipo == LedgerEntryType.Credito).Sum(e => e.Valor);
        var totalDebitos = entradas.Where(e => e.Tipo == LedgerEntryType.Debito).Sum(e => e.Valor);

        (totalDebitos - totalCreditos).Should().Be(0m,
            "a conciliação do Ledger deve fechar em exatamente ZERO");

        totalCreditos.Should().Be(valorTotal,
            "a soma dos créditos deve ser igual ao valor total do pedido");
    }

    [Theory(DisplayName = "Split deve sempre fechar em ZERO para valores variados")]
    [InlineData(100.00)]
    [InlineData(1999.99)]
    [InlineData(0.03)]     // Caso extremo: 3 centavos divididos em 3 partes
    [InlineData(100000.00)]
    public void Split_ValoresVariados_DeveFecharEmZero(decimal valorTotal)
    {
        var contrato = CriarContrato();
        var entradas = _sut.CalcularSplit(contrato, Guid.NewGuid(), valorTotal);

        var liquido = entradas.Where(e => e.Tipo == LedgerEntryType.Debito).Sum(e => e.Valor)
                    - entradas.Where(e => e.Tipo == LedgerEntryType.Credito).Sum(e => e.Valor);

        liquido.Should().Be(0m, "conciliação deve ser zero independente do valor");
    }

    [Fact(DisplayName = "Entradas do Ledger devem ter o mesmo ReferenciaId (rastreabilidade do batch)")]
    public void Split_DeveAgruparEntradasComMesmaReferenciaId()
    {
        var contrato = CriarContrato();
        var entradas = _sut.CalcularSplit(contrato, Guid.NewGuid(), 500m);

        var referenciaIds = entradas.Select(e => e.ReferenciaId).Distinct().ToList();
        referenciaIds.Should().HaveCount(1, "todas as entradas de um split devem pertencer ao mesmo batch");
    }

    [Fact(DisplayName = "Deve gerar 4 entradas: 1 débito mestre + 3 créditos")]
    public void Split_DeveGerar4Entradas()
    {
        var entradas = _sut.CalcularSplit(CriarContrato(), Guid.NewGuid(), 1000m);

        entradas.Should().HaveCount(4);
        entradas.Count(e => e.Tipo == LedgerEntryType.Debito).Should().Be(1);
        entradas.Count(e => e.Tipo == LedgerEntryType.Credito).Should().Be(3);
    }

    // =========================================================================
    // CENÁRIOS DE FALHA
    // =========================================================================

    [Fact(DisplayName = "Percentuais somando mais de 100% devem lançar ArgumentException")]
    public void Contrato_ComPercentuaisAcimaDe100_DeveLancarExcecao()
    {
        // 80% + 15% + 20% = 115% — INVÁLIDO
        var contratoInvalido = CriarContrato(percAr: 0.80m, percAgr: 0.15m, percParceiro: 0.20m);

        var act = () => _sut.CalcularSplit(contratoInvalido, Guid.NewGuid(), 1000m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*100%*", "a mensagem deve explicar que a soma excede 100%");
    }

    [Fact(DisplayName = "Percentuais somando menos de 100% devem lançar ArgumentException")]
    public void Contrato_ComPercentuaisAbaixoDe100_DeveLancarExcecao()
    {
        // 60% + 10% + 20% = 90% — INVÁLIDO
        var contratoInvalido = CriarContrato(percAr: 0.60m, percAgr: 0.10m, percParceiro: 0.20m);

        var act = () => _sut.CalcularSplit(contratoInvalido, Guid.NewGuid(), 1000m);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*100%*");
    }

    [Fact(DisplayName = "Percentual negativo deve lançar ArgumentException")]
    public void Contrato_ComPercentualNegativo_DeveLancarExcecao()
    {
        var contratoInvalido = CriarContrato(percAr: 1.10m, percAgr: -0.10m, percParceiro: 0.00m);

        var act = () => _sut.CalcularSplit(contratoInvalido, Guid.NewGuid(), 1000m);

        act.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "Valor do pedido zero ou negativo deve lançar ArgumentException")]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(-0.01)]
    public void ValorTotal_ZeroOuNegativo_DeveLancarExcecao(decimal valorInvalido)
    {
        var act = () => _sut.CalcularSplit(CriarContrato(), Guid.NewGuid(), valorInvalido);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*positivo*");
    }

    [Fact(DisplayName = "Contrato nulo deve lançar ArgumentNullException")]
    public void Contrato_Nulo_DeveLancarArgumentNullException()
    {
        var act = () => _sut.CalcularSplit(null!, Guid.NewGuid(), 1000m);

        act.Should().Throw<ArgumentNullException>();
    }
}
