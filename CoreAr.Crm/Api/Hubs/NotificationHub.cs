using CoreAr.Identity.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace CoreAr.Crm.Api.Hubs;

/// <summary>
/// Hub central de notificações em tempo real.
///
/// ISOLAMENTO MULTI-TENANT POR DESIGN:
/// Ao conectar, o usuário é automaticamente adicionado a:
///   1. Group "Tenant_{TenantId}"  → Notificações para toda a unidade (AR ou PA)
///   2. Group "User_{UserId}"      → Notificações individuais (só esse usuário vê)
///   3. Group "Role_{Role}"        → Ex: "Role_ROLE_MASTER" para alertas globais
///
/// Assim, um PA NUNCA recebe mensagens de outro PA, pois seus TenantIds diferem.
/// O Master pode ser notificado via Tenant_MasterGuid, Role_ROLE_MASTER ou User_{id}.
/// </summary>
[Authorize] // JWT obrigatório para qualquer conexão
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    // ─── OnConnectedAsync: Adiciona o cliente aos grupos corretos ─────────────
    public override async Task OnConnectedAsync()
    {
        var userId   = Context.UserIdentifier;  // Configurado no Program.cs
        var tenantId = Context.User?.FindFirst(CustomClaims.TenantId)?.Value;
        var roles    = Context.User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];

        if (userId != null && tenantId != null)
        {
            // Grupo individual — apenas este usuário recebe
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");

            // Grupo do Tenant — toda a unidade recebe (ex: PA vê notifiações do seu PA)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Tenant_{tenantId}");

            // Grupos por Role — ex: Master recebe alertas de toda a rede
            foreach (var role in roles)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Role_{role}");
            }

            _logger.LogInformation(
                "SignalR: Usuário {UserId} conectado. Tenant: {TenantId}. ConnectionId: {ConnId}",
                userId, tenantId, Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    // ─── OnDisconnectedAsync: Limpeza automática ──────────────────────────────
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;

        if (exception != null)
        {
            _logger.LogWarning(exception,
                "SignalR: Desconexão com erro. UserId={UserId}, ConnectionId={ConnId}",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "SignalR: Desconexão limpa. UserId={UserId}", userId);
        }

        // Os grupos são limpos automaticamente pelo SignalR ao desconectar
        await base.OnDisconnectedAsync(exception);
    }

    // ─── Método invocável pelo cliente (ex: marcar notificação como lida) ─────
    public async Task MarkAsRead(string notificationId)
    {
        var userId = Context.UserIdentifier;
        _logger.LogInformation(
            "Notificação {NotificationId} marcada como lida por {UserId}",
            notificationId, userId);

        // Confirma de volta ao cliente específico
        await Clients.Caller.SendAsync("NotificationRead", notificationId);
    }
}
