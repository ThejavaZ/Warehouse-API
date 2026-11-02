namespace Api.Auth;

/// <summary>Respuesta de login y refresh: access token corto + refresh token opaco rotatorio.</summary>
public class AuthViewModel
{
    public string AccessToken { get; set; } = null!;

    /// <summary>Segundos que quedan para que venza el access token.</summary>
    public int ExpiresIn { get; set; }

    public string TokenType { get; set; } = "Bearer";

    public string RefreshToken { get; set; } = null!;

    public DateTime RefreshTokenExpiresAt { get; set; }

    public int UserId { get; set; }

    public string Email { get; set; } = null!;
}
