namespace CoreAr.Identity.Domain.Entities;

/// <summary>
/// Refresh Token persistido no banco de dados.
/// 
/// SEGURANÇA CRÍTICA:
/// - O valor armazenado aqui é o HASH SHA-256 do token real.
/// - O token real (valor bruto) trafega APENAS via HttpOnly Cookie.
/// - Isso garante que mesmo uma query SELECT no banco não expõe tokens válidos.
/// </summary>
public class UserRefreshToken
{
    public Guid Id { get; set; }

    // FK para o usuário dono do token
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    // ─── O token em si ───────────────────────────────────────────────────────
    // Armazena APENAS o hash SHA-256 do token real. NUNCA o valor em texto puro.
    public string TokenHash { get; set; } = string.Empty;

    // Identifica o dispositivo/browser para invalide seletiva
    public string DeviceFingerprint { get; set; } = string.Empty; // Ex: User-Agent + IP hash

    // ─── Controle de ciclo de vida ───────────────────────────────────────────
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByIp { get; set; } = string.Empty;

    // Quando usado (rotacionado), o token antigo é invalidado e um novo criado
    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }

    // Aponta para o próximo token após rotação (cadeia de auditoria)
    public Guid? ReplacedByTokenId { get; set; }

    // ─── Computed Properties ─────────────────────────────────────────────────
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;
}
