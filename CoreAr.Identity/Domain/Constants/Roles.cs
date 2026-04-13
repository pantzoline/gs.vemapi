namespace CoreAr.Identity.Domain.Constants;

/// <summary>
/// Definição centralizada das Roles do sistema.
/// Use estas constantes em [Authorize(Roles = Roles.Master)] para evitar magic strings.
/// </summary>
public static class Roles
{
    public const string Master      = "ROLE_MASTER";      // Acesso total
    public const string AdminAr     = "ROLE_ADMIN_AR";    // Gerencia seu AR e PAs
    public const string Pa          = "ROLE_PA";          // Vê apenas seus pedidos
    public const string Agent       = "ROLE_AGR";         // Lança e valida pedidos

    // Grupos para autorização em conjunto (uso em Policies)
    public static readonly string[] ManagementGroup = { Master, AdminAr };
    public static readonly string[] OperationalGroup = { Master, AdminAr, Pa, Agent };
}

/// <summary>
/// Claims customizadas inseridas no JWT além das padrões do Identity.
/// </summary>
public static class CustomClaims
{
    public const string TenantId      = "tenant_id";
    public const string TenantLevel   = "tenant_level";
    public const string ParentTenantId = "parent_tenant_id";
    public const string FullName      = "full_name";
    public const string DocumentNumber = "doc_number";
}
