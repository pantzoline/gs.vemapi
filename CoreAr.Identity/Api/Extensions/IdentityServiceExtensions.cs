using CoreAr.Identity.Application.Services;
using CoreAr.Identity.Domain.Constants;
using CoreAr.Identity.Domain.Entities;
using CoreAr.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;

namespace CoreAr.Identity.Api.Extensions;

/// <summary>
/// Extension methods para manter o Program.cs limpo.
/// Use: builder.Services.AddCoreArIdentity(builder.Configuration)
/// </summary>
public static class IdentityServiceExtensions
{
    public static IServiceCollection AddCoreArIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── 1. DbContext de Identidade ──────────────────────────────────────
        services.AddDbContext<IdentityDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("IdentityDb"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")
            ));

        // ─── 2. ASP.NET Core Identity ────────────────────────────────────────
        services.AddIdentity<ApplicationUser, ApplicationRole>(opts =>
        {
            // Política de senhas robustas
            opts.Password.RequireDigit           = true;
            opts.Password.RequiredLength          = 10;
            opts.Password.RequireUppercase        = true;
            opts.Password.RequireNonAlphanumeric  = true;

            // Bloqueio de conta: após 5 tentativas, bloqueia por 15 minutos
            opts.Lockout.MaxFailedAccessAttempts  = 5;
            opts.Lockout.DefaultLockoutTimeSpan   = TimeSpan.FromMinutes(15);
            opts.Lockout.AllowedForNewUsers       = true;

            // Email único
            opts.User.RequireUniqueEmail          = true;

            // Não exige confirmação de email para MVP (adicionar depois)
            opts.SignIn.RequireConfirmedEmail      = false;
        })
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddDefaultTokenProviders();

        // ─── 3. JWT Authentication ───────────────────────────────────────────
        var jwtSection = configuration.GetSection("Jwt");
        var secretKey  = jwtSection["SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey não configurada!");

        services.AddAuthentication(opts =>
        {
            opts.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = jwtSection["Issuer"],
                ValidAudience            = jwtSection["Audience"],
                IssuerSigningKey         = new SymmetricSecurityKey(
                                              Encoding.UTF8.GetBytes(secretKey)),
                // Tolerância ZERO para expiração (crítico para certificação digital)
                ClockSkew                = TimeSpan.Zero
            };

            // Suporte a JWT via cookie do SignalR Hub (se necessário)
            opts.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    var accessToken = ctx.Request.Query["access_token"];
                    var path = ctx.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        ctx.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });

        // ─── 4. Authorization Policies ───────────────────────────────────────
        services.AddAuthorization(opts =>
        {
            // Política padrão: qualquer usuário autenticado
            opts.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // Políticas granulares por nível hierárquico
            opts.AddPolicy("MasterOnly",
                p => p.RequireRole(Roles.Master));

            opts.AddPolicy("ManagementAndAbove",
                p => p.RequireRole(Roles.Master, Roles.AdminAr));

            opts.AddPolicy("AllPartners",
                p => p.RequireRole(Roles.Master, Roles.AdminAr, Roles.Pa, Roles.Agent));

            // Política customizada: verifica o TenantId no próprio Claim do JWT
            opts.AddPolicy("SameTenantOnly", policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new SameTenantRequirement()));
        });

        // ─── 5. Rate Limiting (prevenção de brute-force no login) ────────────
        services.AddRateLimiter(opts =>
        {
            opts.AddFixedWindowLimiter("login-policy", limiterOpts =>
            {
                limiterOpts.PermitLimit       = 5;   // 5 tentativas
                limiterOpts.Window            = TimeSpan.FromMinutes(1);
                limiterOpts.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOpts.QueueLimit = 0; // Sem fila — rejeita imediatamente
            });

            // Resposta padrão para rate limit excedido
            opts.OnRejected = async (ctx, token) =>
            {
                ctx.HttpContext.Response.StatusCode = 429;
                await ctx.HttpContext.Response.WriteAsJsonAsync(new
                {
                    message = "Muitas tentativas. Aguarde 1 minuto.",
                    retryAfter = 60
                }, token);
            };
        });

        // ─── 6. Registro dos Serviços de Domínio ─────────────────────────────
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}

// Requirement customizado para o Global Query Filter (ver DbContext do CRM/Ledger)
public class SameTenantRequirement : Microsoft.AspNetCore.Authorization.IAuthorizationRequirement { }
