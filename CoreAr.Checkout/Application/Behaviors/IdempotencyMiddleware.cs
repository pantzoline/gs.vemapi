using StackExchange.Redis;

namespace CoreAr.Checkout.Application.Behaviors;

/// <summary>
/// Middleware de Idempotência via Redis Distributed Lock.
///
/// PROBLEMA QUE RESOLVE:
///   Gateways de pagamento podem reenviar webhooks em caso de timeout.
///   Sem idempotência, processaríamos o mesmo pagamento duas vezes → dupla cobrança.
///
/// FLUXO:
///   1. Extrai a chave X-Idempotency-Key do header.
///   2. Tenta adquirir um lock no Redis com TTL de 24h.
///   3. Se o lock já existir (processamento anterior) → retorna 200 imediatamente.
///   4. Se for novo → passa para o próximo middleware/controller.
///   5. Após processamento bem-sucedido → mantém o lock ativo (evita reprocessamento).
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer _redis;
    private const string HEADER_KEY = "X-Idempotency-Key";
    private static readonly TimeSpan TTL = TimeSpan.FromHours(24);

    public IdempotencyMiddleware(RequestDelegate next, IConnectionMultiplexer redis)
    {
        _next = next;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Aplica somente a rotas de webhook
        if (!context.Request.Path.StartsWithSegments("/api/webhooks"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HEADER_KEY, out var idempotencyKey) 
            || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { erro = $"Header '{HEADER_KEY}' é obrigatório." });
            return;
        }

        var redisKey = $"idempotency:{idempotencyKey}";
        var db = _redis.GetDatabase();

        // SET NX (Set if Not eXists) com TTL — operação atômica
        var foiAdquirido = await db.StringSetAsync(
            redisKey,
            "processado",
            TTL,
            When.NotExists);

        if (!foiAdquirido)
        {
            // Já processamos este request → retorna 200 idempotente
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new { mensagem = "Recebido (idempotente)." });
            return;
        }

        // Permite que o body seja lido múltiplas vezes (necessário para validação HMAC)
        context.Request.EnableBuffering();

        try
        {
            await _next(context);
        }
        catch
        {
            // Se o processamento falhou, remove o lock para permitir retry
            await db.KeyDeleteAsync(redisKey);
            throw;
        }
    }
}

// Extensão para registro limpo no Program.cs
public static class IdempotencyMiddlewareExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder app) =>
        app.UseMiddleware<IdempotencyMiddleware>();
}
