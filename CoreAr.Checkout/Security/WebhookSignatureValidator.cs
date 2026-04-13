using System.Security.Cryptography;
using System.Text;

namespace CoreAr.Checkout.Security;

/// <summary>
/// Validador de assinatura digital de webhooks via HMAC-SHA256.
///
/// COMO FUNCIONA:
///   O gateway assina o payload com uma chave secreta compartilhada (por tenant).
///   Nós recalculamos o HMAC e comparamos com o header X-Gateway-Signature.
///   Qualquer divergência → descartamos o request silenciosamente (anti-spoofing).
///
/// ANTI-TIMING-ATTACK:
///   Usamos CryptographicOperations.FixedTimeEquals para evitar 
///   ataques de timing que comparam strings byte a byte.
/// </summary>
public sealed class WebhookSignatureValidator : IWebhookSignatureValidator
{
    private readonly ISegredoGatewayRepository _segredoRepository;

    public WebhookSignatureValidator(ISegredoGatewayRepository segredoRepository)
    {
        _segredoRepository = segredoRepository;
    }

    public async Task<bool> ValidarAsync(string payloadBruto, string assinaturaRecebida, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(assinaturaRecebida))
            return false;

        // Obtém a chave secreta do tenant (armazenada no AWS Secrets Manager / Vault)
        var chaveSecreta = await _segredoRepository.ObterChaveGatewayAsync(tenantId);
        if (chaveSecreta is null)
            return false;

        var chaveBytes = Encoding.UTF8.GetBytes(chaveSecreta);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadBruto);

        using var hmac = new HMACSHA256(chaveBytes);
        var hashCalculado = hmac.ComputeHash(payloadBytes);
        var hashCalculadoHex = Convert.ToHexString(hashCalculado).ToLowerInvariant();

        // Normaliza o header (alguns gateways prefixam com "sha256=")
        var assinaturaNormalizada = assinaturaRecebida
            .Replace("sha256=", "", StringComparison.OrdinalIgnoreCase)
            .ToLowerInvariant();

        var hashRecebidoBytes = Convert.FromHexString(assinaturaNormalizada);
        var hashCalculadoBytes = Convert.FromHexString(hashCalculadoHex);

        // Comparação em tempo constante (anti timing-attack)
        return CryptographicOperations.FixedTimeEquals(hashRecebidoBytes, hashCalculadoBytes);
    }
}

/// <summary>
/// Abstração do repositório de segredos do gateway.
/// Implementação deve buscar do AWS Secrets Manager, HashiCorp Vault etc.
/// NUNCA armazene segredos em appsettings.json em produção.
/// </summary>
public interface ISegredoGatewayRepository
{
    Task<string?> ObterChaveGatewayAsync(Guid tenantId);
}
