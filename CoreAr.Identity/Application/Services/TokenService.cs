using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CoreAr.Identity.Domain.Constants;
using CoreAr.Identity.Domain.Entities;
using CoreAr.Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CoreAr.Identity.Application.Services;

public interface ITokenService
{
    Task<(string AccessToken, string RefreshToken)> GenerateTokenPairAsync(
        ApplicationUser user, string ipAddress, string deviceFingerprint);

    Task<(string AccessToken, string RefreshToken)> RotateRefreshTokenAsync(
        string rawRefreshToken, string ipAddress, string deviceFingerprint);

    Task RevokeAllUserTokensAsync(Guid userId, string reason);
}

/// <summary>
/// Motor de geração e rotação de tokens JWT + Refresh Token.
/// 
/// Fluxo de Segurança:
/// 1. Access Token (15 min)  → gerado a cada login e a cada refresh
/// 2. Refresh Token (7 dias) → rotacionado a cada uso (o antigo é revogado)
/// 3. Token Hash (SHA-256)   → o que é gravado no banco (NUNCA o valor bruto)
/// 4. HttpOnly Cookie        → como o Refresh Token chega ao frontend
/// </summary>
public class TokenService : ITokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IdentityDbContext _db;

    public TokenService(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        IdentityDbContext db)
    {
        _config     = config;
        _userManager = userManager;
        _db         = db;
    }

    // ─── GERAÇÃO DO PAR DE TOKENS (usado no Login) ───────────────────────────
    public async Task<(string AccessToken, string RefreshToken)> GenerateTokenPairAsync(
        ApplicationUser user, string ipAddress, string deviceFingerprint)
    {
        var accessToken = await BuildAccessTokenAsync(user);
        var (rawRefreshToken, hashedToken) = GenerateRefreshToken();

        // Limpa tokens expirados do mesmo device antes de criar novo (housekeeping)
        var oldTokens = await _db.RefreshTokens
            .Where(t => t.UserId == user.Id && t.DeviceFingerprint == deviceFingerprint && t.IsExpired)
            .ToListAsync();
        _db.RefreshTokens.RemoveRange(oldTokens);

        // Persiste apenas o HASH — o token bruto vai pro cookie
        var refreshTokenEntity = new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            DeviceFingerprint = deviceFingerprint,
            ExpiresAt = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7)),
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync();

        return (accessToken, rawRefreshToken);
    }

    // ─── ROTAÇÃO DO REFRESH TOKEN (usado no /refresh-token) ─────────────────
    public async Task<(string AccessToken, string RefreshToken)> RotateRefreshTokenAsync(
        string rawRefreshToken, string ipAddress, string deviceFingerprint)
    {
        var tokenHash = HashToken(rawRefreshToken);

        var storedToken = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (storedToken == null)
            throw new SecurityTokenException("Refresh token inválido.");

        // Ataque de Replay Detection: token já revogado = possível comprometimento
        if (storedToken.IsRevoked)
        {
            // Invalida TODOS os tokens do usuário como medida de segurança
            await RevokeAllUserTokensAsync(storedToken.UserId, "Replay attack detected");
            throw new SecurityTokenException("Token revogado. Sessão encerrada por segurança.");
        }

        if (storedToken.IsExpired)
            throw new SecurityTokenException("Refresh token expirado. Faça login novamente.");

        // Verifica se o device bate (proteção adicional contra roubo de cookie)
        if (storedToken.DeviceFingerprint != deviceFingerprint)
            throw new SecurityTokenException("Device não reconhecido.");

        // Gera o novo par
        var (newRawRefreshToken, newHashedToken) = GenerateRefreshToken();
        var newAccessToken = await BuildAccessTokenAsync(storedToken.User);

        var newRefreshTokenEntity = new UserRefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = newHashedToken,
            DeviceFingerprint = deviceFingerprint,
            ExpiresAt = DateTime.UtcNow.AddDays(
                _config.GetValue<int>("Jwt:RefreshTokenExpirationDays", 7)),
            CreatedByIp = ipAddress
        };

        // Revoga o token antigo e aponta para o novo (cadeia de auditoria)
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = ipAddress;
        storedToken.ReplacedByTokenId = newRefreshTokenEntity.Id;

        _db.RefreshTokens.Add(newRefreshTokenEntity);
        await _db.SaveChangesAsync();

        // Atualiza LastLoginAt do usuário
        storedToken.User.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(storedToken.User);

        return (newAccessToken, newRawRefreshToken);
    }

    // ─── REVOGAÇÃO TOTAL (Logout ou violação de segurança) ───────────────────
    public async Task RevokeAllUserTokensAsync(Guid userId, string reason)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = $"SYSTEM:{reason}";
        }

        await _db.SaveChangesAsync();
    }

    // ─── CONSTRUÇÃO DO ACCESS TOKEN (JWT) ────────────────────────────────────
    private async Task<string> BuildAccessTokenAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var secretKey = _config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey não configurada.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims que ficam no JWT — lidos pelo middleware de autorização
        var claims = new List<Claim>
        {
            // Claims padrão
            new(JwtRegisteredClaimNames.Sub,    user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email,  user.Email ?? ""),
            new(JwtRegisteredClaimNames.Jti,    Guid.NewGuid().ToString()), // JWT ID único
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),

            // Claims de negócio (Multi-Tenant)
            new(CustomClaims.TenantId,       user.TenantId.ToString()),
            new(CustomClaims.TenantLevel,    ((int)user.TenantLevel).ToString()),
            new(CustomClaims.FullName,       user.FullName),
            new(CustomClaims.DocumentNumber, user.DocumentNumber),
        };

        // Adiciona o ParentTenantId se existir (PA → AR, AR → Master)
        if (user.ParentTenantId.HasValue)
            claims.Add(new(CustomClaims.ParentTenantId, user.ParentTenantId.Value.ToString()));

        // Adiciona todas as Roles do usuário como claims
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var expirationMinutes = _config.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 15);

        var token = new JwtSecurityToken(
            issuer:   _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims:   claims,
            expires:  DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ─── UTILITÁRIOS ─────────────────────────────────────────────────────────
    private static (string RawToken, string HashedToken) GenerateRefreshToken()
    {
        // Gera 64 bytes aleatórios criptograficamente seguros (512 bits de entropia)
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(randomBytes);
        return (rawToken, HashToken(rawToken));
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
