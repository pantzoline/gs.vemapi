using CoreAr.Checkout.Application.Behaviors;
using CoreAr.Checkout.Api.Controllers;
using CoreAr.Checkout.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

// ============================================================
// CORE-AR — Checkout API
// Ponto de entrada da aplicação. Registra todos os serviços,
// middlewares e configura o pipeline HTTP.
// ============================================================

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

// -----------------------------------------------------------
// 1. AUTENTICAÇÃO JWT (Auth0 / Keycloak)
// -----------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // A Authority é o URL do seu tenant no Auth0 ou Keycloak
        options.Authority = config["Auth:Authority"];
        options.Audience  = config["Auth:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ClockSkew                = TimeSpan.FromSeconds(30) // tolerância mínima
        };
    });

builder.Services.AddAuthorization();

// -----------------------------------------------------------
// 2. REDIS (Idempotência + Distributed Locks)
// -----------------------------------------------------------
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(config.GetConnectionString("Redis")
        ?? throw new InvalidOperationException("Redis connection string não configurada.")));

// -----------------------------------------------------------
// 3. HTTP CLIENTS (Gateways externos)
// -----------------------------------------------------------
builder.Services.AddHttpClient("FocusNfe", client =>
{
    client.BaseAddress = new Uri(config["FocusNfe:BaseUrl"]
        ?? "https://api.focusnfe.com.br/v2/");
    client.DefaultRequestHeaders.Add("Authorization",
        $"Token {config["FocusNfe:ApiKey"]}");
});

builder.Services.AddHttpClient("ZAPI", client =>
{
    client.BaseAddress = new Uri(config["ZAPI:BaseUrl"]
        ?? throw new InvalidOperationException("ZAPI BaseUrl não configurada."));
    client.DefaultRequestHeaders.Add("Client-Token", config["ZAPI:ClientToken"]);
});

builder.Services.AddHttpClient("SendGrid", client =>
{
    client.BaseAddress = new Uri("https://api.sendgrid.com/");
    client.DefaultRequestHeaders.Add("Authorization",
        $"Bearer {config["SendGrid:ApiKey"]}");
});

// -----------------------------------------------------------
// 4. SERVIÇOS DE DOMÍNIO
// -----------------------------------------------------------
builder.Services.AddScoped<IWebhookSignatureValidator, WebhookSignatureValidator>();
builder.Services.AddScoped<ISegredoGatewayRepository, SegredoGatewayRepositoryPlaceholder>();
builder.Services.AddScoped<IOrderEventPublisher, RabbitMqOrderEventPublisher>();

// -----------------------------------------------------------
// 5. CONTROLLERS + SWAGGER
// -----------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title   = "CORE-AR — Checkout API",
        Version = "v1",
        Description = "Motor de Checkout e Webhooks para o ERP SaaS ICP-Brasil."
    });
    // Adiciona suporte a Bearer Token no Swagger UI
    c.AddSecurityDefinition("Bearer", new()
    {
        Type   = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});

// -----------------------------------------------------------
// 6. HEALTH CHECKS
// -----------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddNpgSql(config.GetConnectionString("Postgres")!, name: "postgres")
    .AddRedis(config.GetConnectionString("Redis")!, name: "redis");

// ============================================================
// BUILD DO APP + PIPELINE HTTP
// ============================================================
var app = builder.Build();

// Ambiente de Desenvolvimento: expõe Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "CORE-AR Checkout v1");
        c.RoutePrefix = string.Empty; // Swagger na raiz: http://localhost:5000/
    });
}

app.UseHttpsRedirection();

// Middleware de Idempotência ANTES dos controllers
// Intercepta webhooks duplicados antes de qualquer processamento
app.UseIdempotency();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// ============================================================
// PLACEHOLDERS — Substitua por implementações reais
// ============================================================

/// <summary>
/// PLACEHOLDER: Busca chave HMAC do gateway.
/// Em produção: injetar AWS Secrets Manager ou HashiCorp Vault.
/// </summary>
public class SegredoGatewayRepositoryPlaceholder : ISegredoGatewayRepository
{
    private readonly IConfiguration _config;
    public SegredoGatewayRepositoryPlaceholder(IConfiguration config) => _config = config;
    public Task<string?> ObterChaveGatewayAsync(Guid tenantId) =>
        Task.FromResult(_config[$"Gateways:WebhookSecret:{tenantId}"]);
}

/// <summary>
/// PLACEHOLDER: Publicador de eventos via RabbitMQ.
/// Em produção: usar MassTransit ou RabbitMQ.Client diretamente.
/// </summary>
public class RabbitMqOrderEventPublisher : IOrderEventPublisher
{
    private readonly ILogger<RabbitMqOrderEventPublisher> _logger;
    public RabbitMqOrderEventPublisher(ILogger<RabbitMqOrderEventPublisher> logger) =>
        _logger = logger;

    public Task PublicarAsync(CoreAr.Ledger.Domain.Events.OrderPaidEvent evento, CancellationToken ct)
    {
        _logger.LogInformation(
            "[RabbitMQ] Publicando OrderPaidEvent: PedidoId={PedidoId}, Valor={Valor}",
            evento.PedidoId, evento.ValorTotal);
        // TODO: Implementar publish real no RabbitMQ
        return Task.CompletedTask;
    }
}
