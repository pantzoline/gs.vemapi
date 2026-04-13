using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CoreAr.Crm.Workers;

/// <summary>
/// Worker Diário de Vencimento de Certificados.
///
/// LÓGICA:
///   Executa uma vez por dia (configurável via appsettings).
///   Varre o banco buscando certificados com status 'Ativo'
///   nos gatilhos D-30, D-15 e D-7 antes da data de expiração.
///   Para cada certificado encontrado, enfileira uma notificação.
///
/// ÍNDICE CRÍTICO UTILIZADO:
///   idx_cert_expiracao_status ON certificados(status, data_expiracao)
///   WHERE status = 'Ativo'
///   (Definido no InitCrmSchema.sql — evita full table scan)
/// </summary>
public sealed class VencimentoCertificadoCronWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<VencimentoCertificadoCronWorker> _logger;
    private readonly TimeSpan _intervalo = TimeSpan.FromHours(24);

    private static readonly int[] GATILHOS_DIAS = [30, 15, 7];

    public VencimentoCertificadoCronWorker(
        IServiceProvider services,
        ILogger<VencimentoCertificadoCronWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker de vencimento de certificados iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ExecutarVarreduraAsync(stoppingToken);
            await Task.Delay(_intervalo, stoppingToken);
        }
    }

    private async Task ExecutarVarreduraAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando varredura de certificados a vencer. Data={Data}", DateTime.UtcNow.Date);

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        var notificacaoService = scope.ServiceProvider.GetRequiredService<INotificacaoService>();

        foreach (var dias in GATILHOS_DIAS)
        {
            await ProcessarGatilhoAsync(db, notificacaoService, dias, cancellationToken);
        }
    }

    private async Task ProcessarGatilhoAsync(
        NpgsqlDataSource db,
        INotificacaoService notificacaoService,
        int diasAteVencimento,
        CancellationToken cancellationToken)
    {
        var dataAlvo = DateTime.UtcNow.Date.AddDays(diasAteVencimento);

        // Query usa o índice composto idx_cert_expiracao_status
        const string sql = """
            SELECT c.id, c.tenant_id, c.titular_nome, c.titular_cpf, c.tipo,
                   c.data_expiracao, c.link_renovacao
            FROM certificados c
            WHERE c.status = 'Ativo'
              AND c.data_expiracao = @dataAlvo
              AND NOT EXISTS (
                  -- Evita reenvio se já notificamos hoje para este gatilho
                  SELECT 1 FROM notificacoes_renovacao nr
                  WHERE nr.certificado_id = c.id
                    AND nr.gatilho_dias = @dias
                    AND nr.status = 'Enviado'
              )
            """;

        await using var conn = await db.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("dataAlvo", dataAlvo);
        cmd.Parameters.AddWithValue("dias", diasAteVencimento);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        int total = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var certificadoId = reader.GetGuid(0);
            var tenantId = reader.GetGuid(1);
            var nome = reader.GetString(2);
            var linkRenovacao = reader.IsDBNull(6) ? null : reader.GetString(6);

            await notificacaoService.EnfileirarNotificacaoAsync(new OrdemNotificacaoDto(
                CertificadoId: certificadoId,
                TenantId: tenantId,
                NomeTitular: nome,
                DiasRestantes: diasAteVencimento,
                LinkRenovacao: linkRenovacao ?? $"https://portal.gs.vemapi.com.br/renovar/{certificadoId}"
            ), cancellationToken);

            total++;
        }

        _logger.LogInformation(
            "Gatilho D-{Dias}: {Total} certificados enfileirados para notificação. Data alvo={DataAlvo}",
            diasAteVencimento, total, dataAlvo);
    }
}

public sealed record OrdemNotificacaoDto(
    Guid CertificadoId,
    Guid TenantId,
    string NomeTitular,
    int DiasRestantes,
    string LinkRenovacao
);

public interface INotificacaoService
{
    Task EnfileirarNotificacaoAsync(OrdemNotificacaoDto ordem, CancellationToken cancellationToken);
}
