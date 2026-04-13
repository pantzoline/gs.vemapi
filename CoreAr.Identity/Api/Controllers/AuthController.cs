using CoreAr.Identity.Application.Services;
using CoreAr.Identity.Domain.Constants;
using CoreAr.Identity.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;

namespace CoreAr.Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    // Configuração dos Cookies: duração e flags HttpOnly
    private static readonly CookieOptions RefreshTokenCookieOptions = new()
    {
        HttpOnly  = true,   // NUNCA acessível via JavaScript (anti-XSS)
        Secure    = true,   // Apenas HTTPS em produção
        SameSite  = SameSiteMode.Strict,
        Path      = "/api/auth/refresh-token", // Escopo mínimo do cookie
        MaxAge    = TimeSpan.FromDays(7)
    };

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthController> logger)
    {
        _userManager  = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger       = logger;
    }

    // ─── POST /api/auth/login ─────────────────────────────────────────────────
    /// <summary>
    /// Autentica o usuário e retorna o Access Token + define o Refresh Token via cookie.
    /// Rate limit: 5 tentativas por minuto por IP para prevenção de brute-force.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login-policy")] // Definido via AddRateLimiter no Program.cs
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user == null || !user.IsActive)
        {
            // NÃO revele se o email existe ou não (segurança)
            _logger.LogWarning("Tentativa de login falhou para: {Email}", request.Email);
            return Unauthorized(new { message = "Credenciais inválidas." });
        }

        // Verifica bloqueio (Identity.AccountLockout cuida disso)
        if (await _userManager.IsLockedOutAsync(user))
        {
            _logger.LogWarning("Conta bloqueada tentou login: UserId={UserId}", user.Id);
            return StatusCode(423, new
            {
                message = "Conta temporariamente bloqueada.",
                lockedUntil = user.LockoutEnd
            });
        }

        var signInResult = await _signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true); // Incrementa contador de falhas

        if (!signInResult.Succeeded)
        {
            var remainingAttempts = _userManager.Options.Lockout.MaxFailedAccessAttempts
                                    - await _userManager.GetAccessFailedCountAsync(user);

            _logger.LogWarning(
                "Senha incorreta para UserId={UserId}. Tentativas restantes: {Remaining}",
                user.Id, remainingAttempts);

            return Unauthorized(new
            {
                message = "Credenciais inválidas.",
                attemptsRemaining = remainingAttempts
            });
        }

        // Gera par de tokens
        var ipAddress         = GetClientIp();
        var deviceFingerprint = GetDeviceFingerprint();

        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(
            user, ipAddress, deviceFingerprint);

        // Atualiza LastLogin
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        // Zera o contador de tentativas falhas após login bem-sucedido
        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation("Login bem-sucedido: UserId={UserId}, Role={Role}",
            user.Id, string.Join(",", await _userManager.GetRolesAsync(user)));

        // O Refresh Token vai APENAS no cookie HttpOnly — nunca no body
        Response.Cookies.Append("X-Refresh-Token", refreshToken, RefreshTokenCookieOptions);

        return Ok(new LoginResponse(
            AccessToken: accessToken,
            ExpiresIn:   15 * 60, // 900 segundos
            User: new UserSummary(
                Id:          user.Id,
                FullName:    user.FullName,
                Email:       user.Email ?? "",
                TenantId:   user.TenantId,
                TenantLevel: user.TenantLevel.ToString()
            )
        ));
    }

    // ─── POST /api/auth/refresh-token ─────────────────────────────────────────
    /// <summary>
    /// Silently rotates the refresh token and returns a new access token.
    /// The old refresh token is immediately invalidated (Token Rotation).
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken()
    {
        var rawRefreshToken = Request.Cookies["X-Refresh-Token"];

        if (string.IsNullOrEmpty(rawRefreshToken))
            return Unauthorized(new { message = "Refresh token não encontrado." });

        try
        {
            var ipAddress         = GetClientIp();
            var deviceFingerprint = GetDeviceFingerprint();

            var (newAccessToken, newRefreshToken) = await _tokenService.RotateRefreshTokenAsync(
                rawRefreshToken, ipAddress, deviceFingerprint);

            // Substitui o cookie com o novo refresh token
            Response.Cookies.Append("X-Refresh-Token", newRefreshToken, RefreshTokenCookieOptions);

            return Ok(new { accessToken = newAccessToken, expiresIn = 900 });
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex)
        {
            // Remove o cookie inválido do browser
            Response.Cookies.Delete("X-Refresh-Token");
            _logger.LogWarning("Refresh token inválido: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
    }

    // ─── POST /api/auth/logout ────────────────────────────────────────────────
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out var userId))
        {
            await _tokenService.RevokeAllUserTokensAsync(userId, "User logout");
        }

        Response.Cookies.Delete("X-Refresh-Token");
        return NoContent();
    }

    // ─── GET /api/auth/me ─────────────────────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        // Lê diretamente do JWT — sem query no banco
        return Ok(new
        {
            UserId      = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Email       = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            FullName    = User.FindFirst(Domain.Constants.CustomClaims.FullName)?.Value,
            TenantId    = User.FindFirst(Domain.Constants.CustomClaims.TenantId)?.Value,
            TenantLevel = User.FindFirst(Domain.Constants.CustomClaims.TenantLevel)?.Value,
            Roles       = User.FindAll(System.Security.Claims.ClaimTypes.Role)
                              .Select(c => c.Value).ToArray()
        });
    }

    // ─── Exemplo de endpoint protegido por Role ───────────────────────────────
    [HttpGet("admin-only")]
    [Authorize(Roles = $"{Roles.Master},{Roles.AdminAr}")]
    public IActionResult MasterOnlyEndpoint() => Ok("Acesso concedido ao Administrador.");

    // ─── Utilitários privados ─────────────────────────────────────────────────
    private string GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private string GetDeviceFingerprint()
    {
        // Cria um fingerprint a partir de headers para vincular tokens ao device
        var userAgent = Request.Headers.UserAgent.ToString();
        var accept = Request.Headers.Accept.ToString();
        var raw = $"{userAgent}|{accept}";
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes)[..16]; // 16 chars é suficiente
    }
}

// ─── DTOs ──────────────────────────────────────────────────────────────────────
public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

public record LoginResponse(
    string AccessToken,
    int ExpiresIn,
    UserSummary User
);

public record UserSummary(
    Guid Id,
    string FullName,
    string Email,
    Guid TenantId,
    string TenantLevel
);
