using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreAr.Billing.Workers;

/// <summary>
/// Consumer RabbitMQ — Emissão de Notas Fiscais.
///
/// FLUXO ASSÍNCRONO:
///   1. Escuta a fila 'billing.emitir-notas' no RabbitMQ.
///   2. Para cada mensagem, extrai o PedidoFaturamentoDto.
///   3. Dispara NFS-e (Serviço → Prefeitura via Focus NFe/eNotas).
///   4. Dispara NF-e (Produto → SEFAZ via Focus NFe/eNotas).
///   5. Em caso de falha, reenfileira na Dead Letter Queue (DLQ).
///   6. Confirma (ACK) somente após ambas emissões bem-sucedidas.
///
/// TRIBUTAÇÃO HÍBRIDA DE CERTIFICADOS DIGITAIS:
///   - Serviço de Validação → NFS-e (ISS Municipal)
///   - Token/Smartcard Físico → NF-e (ICMS Estadual via SEFAZ)
/// </summary>
public sealed class EmitirNotasFiscaisConsumer : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectionFactory _rabbitConnectionFactory;
    private readonly ILogger<EmitirNotasFiscaisConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    private const string FILA_ENTRADA = "billing.emitir-notas";
    private const string FILA_DLQ = "billing.emitir-notas.dlq";
    private const string FOCUS_NFE_BASE_URL = "https://api.focusnfe.com.br/v2";

    public EmitirNotasFiscaisConsumer(
        IHttpClientFactory httpClientFactory,
        IConnectionFactory rabbitConnectionFactory,
        ILogger<EmitirNotasFiscaisConsumer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _rabbitConnectionFactory = rabbitConnectionFactory;
        _logger = logger;
    }

    public void Iniciar()
    {
        _connection = _rabbitConnectionFactory.CreateConnection();
        _channel = _connection.CreateModel();

        // Configura DLQ antes da fila principal
        _channel.QueueDeclare(FILA_DLQ, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(
            queue: FILA_ENTRADA,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                { "x-dead-letter-exchange", "" },
                { "x-dead-letter-routing-key", FILA_DLQ }
            });

        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, args) => await ProcessarAsync(args);

        _channel.BasicConsume(FILA_ENTRADA, autoAck: false, consumer);
        _logger.LogInformation("Consumer de emissão de notas fiscais iniciado na fila '{Fila}'", FILA_ENTRADA);
    }

    private async Task ProcessarAsync(BasicDeliverEventArgs args)
    {
        PedidoFaturamentoDto? pedido = null;

        try
        {
            pedido = JsonSerializer.Deserialize<PedidoFaturamentoDto>(args.Body.Span);

            if (pedido is null)
                throw new InvalidOperationException("Payload de faturamento inválido ou nulo.");

            _logger.LogInformation("Processando faturamento do PedidoId={PedidoId}", pedido.PedidoId);

            // Dispara NFS-e e NF-e em paralelo (são independentes)
            await Task.WhenAll(
                EmitirNfseAsync(pedido),    // Serviço (ISS)
                EmitirNfeAsync(pedido)      // Produto - apenas se tiver item físico
            );

            _channel!.BasicAck(args.DeliveryTag, multiple: false);
            _logger.LogInformation("Notas fiscais emitidas com sucesso para PedidoId={PedidoId}", pedido.PedidoId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao emitir nota fiscal para PedidoId={PedidoId}. Enviando para DLQ.",
                pedido?.PedidoId);

            // NACK sem requeue → vai para DLQ para análise manual
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
    }

    /// <summary>Emite Nota Fiscal de Serviço Eletrônica (NFS-e) via Focus NFe.</summary>
    private async Task EmitirNfseAsync(PedidoFaturamentoDto pedido)
    {
        var http = _httpClientFactory.CreateClient("FocusNfe");
        var payload = new
        {
            data_emissao = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            prestador = new { cnpj = pedido.CnpjAr, inscricao_municipal = pedido.InscricaoMunicipalAr },
            tomador = new { cnpj = pedido.CnpjCliente, razao_social = pedido.NomeCliente },
            servico = new
            {
                aliquota = 0.05m,                       // ISS 5% (varia por município)
                discriminacao = "Serviço de validação e emissão de Certificado Digital ICP-Brasil",
                valor_servicos = pedido.ValorServico
            }
        };

        var response = await http.PostAsJsonAsync($"{FOCUS_NFE_BASE_URL}/nfse?ref={pedido.PedidoId}", payload);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Emite Nota Fiscal Eletrônica (NF-e) para itens físicos (Token/Smartcard).</summary>
    private async Task EmitirNfeAsync(PedidoFaturamentoDto pedido)
    {
        if (pedido.ValorProduto <= 0)
        {
            _logger.LogInformation("Pedido {PedidoId} não possui produto físico. NF-e não emitida.", pedido.PedidoId);
            return;
        }

        var http = _httpClientFactory.CreateClient("FocusNfe");
        var payload = new
        {
            natureza_operacao = "Venda de mercadoria",
            data_emissao = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            emitente = new { cnpj = pedido.CnpjAr },
            destinatario = new { cpf_cnpj = pedido.CnpjCliente, nome = pedido.NomeCliente },
            itens = new[]
            {
                new
                {
                    numero_item = "1",
                    codigo_produto = "SMARTCARD-A3",
                    descricao = "Token/Smartcard para Certificado Digital A3",
                    ncm = "84717090",
                    quantidade = 1,
                    valor_unitario = pedido.ValorProduto,
                    valor_total = pedido.ValorProduto
                }
            }
        };

        var response = await http.PostAsJsonAsync($"{FOCUS_NFE_BASE_URL}/nfe?ref={pedido.PedidoId}-produto", payload);
        response.EnsureSuccessStatusCode();
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}

public sealed record PedidoFaturamentoDto(
    Guid PedidoId,
    Guid TenantId,
    string CnpjAr,
    string InscricaoMunicipalAr,
    string CnpjCliente,
    string NomeCliente,
    decimal ValorServico,   // Parte do valor referente ao serviço de validação
    decimal ValorProduto    // Parte referente ao token físico (pode ser 0)
);
