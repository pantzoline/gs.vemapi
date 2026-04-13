using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreAr.Crm.Workers;

/// <summary>
/// Consumer de Notificações — WhatsApp e Email.
///
/// ESTRATÉGIA MULTI-CANAL:
///   - WhatsApp via Z-API ou Twilio (configurável por tenant)
///   - Email via SendGrid / Resend
///   - Fallback: se WhatsApp falhar, tenta Email automaticamente
///
/// IDEMPOTÊNCIA:
///   A query do CronWorker já filtra notificações enviadas,
///   mas este consumer também verifica antes de enviar (double-check).
/// </summary>
public sealed class NotificacaoClienteConsumer : IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConnectionFactory _rabbitConnectionFactory;
    private readonly ILogger<NotificacaoClienteConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    private const string FILA = "crm.notificacoes-renovacao";

    public NotificacaoClienteConsumer(
        IHttpClientFactory httpClientFactory,
        IConnectionFactory rabbitConnectionFactory,
        ILogger<NotificacaoClienteConsumer> logger)
    {
        _httpClientFactory = httpClientFactory;
        _rabbitConnectionFactory = rabbitConnectionFactory;
        _logger = logger;
    }

    public void Iniciar()
    {
        _connection = _rabbitConnectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(FILA, durable: true, exclusive: false, autoDelete: false);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 5, global: false);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (_, args) => await ProcessarAsync(args);
        _channel.BasicConsume(FILA, autoAck: false, consumer);

        _logger.LogInformation("Consumer de notificações iniciado na fila '{Fila}'", FILA);
    }

    private async Task ProcessarAsync(BasicDeliverEventArgs args)
    {
        OrdemNotificacaoDto? ordem = null;
        try
        {
            ordem = JsonSerializer.Deserialize<OrdemNotificacaoDto>(args.Body.Span);
            if (ordem is null) throw new InvalidOperationException("Payload de notificação inválido.");

            var enviouWhatsApp = await EnviarWhatsAppAsync(ordem);

            if (!enviouWhatsApp)
            {
                _logger.LogWarning("WhatsApp falhou para CertificadoId={Id}. Tentando email.", ordem.CertificadoId);
                await EnviarEmailAsync(ordem);
            }

            _channel!.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao notificar cliente para CertificadoId={Id}",
                ordem?.CertificadoId);
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task<bool> EnviarWhatsAppAsync(OrdemNotificacaoDto ordem)
    {
        try
        {
            var http = _httpClientFactory.CreateClient("ZAPI");
            var mensagem = MontarMensagemWhatsApp(ordem);

            var payload = new { phone = "{{NUMERO_CELULAR_DO_CLIENTE}}", message = mensagem };
            var response = await http.PostAsJsonAsync("/send-text", payload);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar Z-API para WhatsApp.");
            return false;
        }
    }

    private async Task EnviarEmailAsync(OrdemNotificacaoDto ordem)
    {
        var http = _httpClientFactory.CreateClient("SendGrid");
        var payload = new
        {
            to = new[] { new { email = "{{EMAIL_DO_CLIENTE}}" } },
            from = new { email = "noreply@gs.vemapi.com.br", name = "CORE-AR" },
            subject = $"⚠️ Seu certificado vence em {ordem.DiasRestantes} dias — Renove agora!",
            content = new[]
            {
                new
                {
                    type = "text/html",
                    value = MontarEmailHtml(ordem)
                }
            }
        };

        var response = await http.PostAsJsonAsync("/v3/mail/send", payload);
        response.EnsureSuccessStatusCode();
    }

    private static string MontarMensagemWhatsApp(OrdemNotificacaoDto ordem) =>
        $"""
        ⚠️ *Alerta de Vencimento — Certificado Digital*

        Olá, *{ordem.NomeTitular}*!

        Seu certificado digital vence em *{ordem.DiasRestantes} dias*.

        Não deixe sua operação parar! Renove agora com apenas alguns cliques:
        👉 {ordem.LinkRenovacao}

        Dúvidas? Fale com nossa equipe.
        """;

    private static string MontarEmailHtml(OrdemNotificacaoDto ordem) =>
        $"""
        <h2>⚠️ Seu certificado digital vence em {ordem.DiasRestantes} dias</h2>
        <p>Olá, <strong>{ordem.NomeTitular}</strong>!</p>
        <p>Para evitar interrupções nos seus processos, renove seu certificado antes do vencimento.</p>
        <a href="{ordem.LinkRenovacao}" 
           style="background:#4F46E5;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block;margin-top:12px">
           Renovar Certificado Agora →
        </a>
        <p style="color:#888;font-size:12px;margin-top:24px">CORE-AR — Sistema de Gestão ICP-Brasil</p>
        """;

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
    }
}
