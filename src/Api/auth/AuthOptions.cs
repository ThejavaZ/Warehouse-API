namespace Api.Auth;

/// <summary>
/// Opciones de autenticación leídas de la sección "Auth" de la configuración.
/// El secreto es obligatorio (mínimo 32 caracteres) y nunca se versiona:
/// en desarrollo se guarda con user-secrets y en despliegue con la variable de entorno Auth__Secret.
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Secreto HMAC-SHA256 con el que se firman los JWT. Obligatorio.</summary>
    public string? Secret { get; set; }

    public string Issuer { get; set; } = "WarehouseApi";

    public string Audience { get; set; } = "WarehouseApi.Client";

    /// <summary>Vida del access token en minutos; corta a propósito para limitar el daño de un token robado.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>Vida del refresh token en días.</summary>
    public int RefreshTokenDays { get; set; } = 7;

    /// <summary>Permite el registro abierto <c>POST /api/Auth/register</c>.</summary>
    public bool RegistrationEnabled { get; set; } = true;
}
