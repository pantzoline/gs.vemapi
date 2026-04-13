using CoreAr.Crm.Api.Hubs;
using CoreAr.Crm.Application.Notifications;
using CoreAr.Identity.Domain.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CoreAr.Crm.Api.Extensions;

public static class SignalRServiceExtensions
{
    /// <summary>
    /// Registra SignalR com suporte a JWT e fallback para Long Polling.
    /// Uso: builder.Services.AddCoreArSignalR()
    /// </summary>
    public static IServiceCollection AddCoreArSignalR(this IServiceCollection services)
    {
        services.AddSignalR(opts =>
        {
            // Tempo máximo sem mensagem antes de considerar cliente desconectado
            opts.ClientTimeoutInterval       = TimeSpan.FromSeconds(60);
            opts.KeepAliveInterval           = TimeSpan.FromSeconds(30);

            // Limita o tamanho de mensagem (proteção contra flood)
            opts.MaximumReceiveMessageSize   = 32 * 1024; // 32 KB

            // Habilita streaming de erros detalhados apenas em Development
            opts.EnableDetailedErrors        = false; // Override no Program.cs para dev
        })
        .AddJsonProtocol(jsonOpts =>
        {
            // camelCase nos nomes das propriedades para o frontend JS
            jsonOpts.PayloadSerializerOptions.PropertyNamingPolicy =
                System.Text.Json.JsonNamingPolicy.CamelCase;
        });

        // Registra o NotificationService como Singleton (IHubContext é thread-safe)
        services.AddSingleton<INotificationService, NotificationService>();

        return services;
    }

    /// <summary>
    /// Mapeia o hub e configura o JWT para WebSocket (tokens via query string para o Hub).
    /// O SignalR no browser não pode enviar Authorization header via WebSocket nativo,
    /// então o token é passado como query parameter e validado aqui.
    /// </summary>
    public static WebApplication MapCoreArSignalR(this WebApplication app)
    {
        // Configura o JWT para aceitar token via query string nos Hubs
        // (O frontend envia: /hubs/notifications?access_token=eyJ...)
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/hubs"))
            {
                var token = context.Request.Query["access_token"].ToString();
                if (!string.IsNullOrEmpty(token))
                {
                    context.Request.Headers.Authorization = $"Bearer {token}";
                }
            }
            await next();
        });

        app.MapHub<NotificationHub>("/hubs/notifications", opts =>
        {
            // Fallback automático: WebSocket → Server-Sent Events → Long Polling
            // Redes corporativas frequentemente bloqueiam WS — SSE e LP garantem funcionalidade
            opts.Transports =
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents |
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
        });

        return app;
    }
}
