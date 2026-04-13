using CoreAr.Identity.Domain.Constants;
using Microsoft.AspNetCore.Identity;
using CoreAr.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using CoreAr.Identity.Infrastructure.Data;

/// <summary>
/// Ponto de entrada do serviço CoreAr.Identity.
/// Responsável por configurar e registrar toda a infraestrutura de Identity.
/// </summary>

var builder = WebApplication.CreateBuilder(args);

// ─── Serviços ────────────────────────────────────────────────────────────────
builder.Services.AddCoreArIdentity(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "CoreAr.Identity API", Version = "v1" });
    // Adiciona suporte ao Bearer token no Swagger UI
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        BearerFormat = "JWT", Scheme = "bearer",
        Description = "Insira o JWT no campo abaixo."
    });
    c.AddSecurityRequirement(new()
    {
        [new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
            Id = "Bearer" } }] = []
    });
});

var app = builder.Build();

// ─── Middleware Pipeline (ORDEM É CRÍTICA) ────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    
    // Auto-apply migrations em desenvolvimento
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseRateLimiter();         // Deve vir antes de UseAuthentication
app.UseAuthentication();      // 1. Valida o JWT
app.UseAuthorization();       // 2. Verifica as Roles/Policies após autenticação
app.MapControllers();

app.Run();
