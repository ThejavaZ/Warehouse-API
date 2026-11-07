using WMS.Domain.Entities;

namespace WMS.Application.Auth;

public interface IJwtTokenService
{
    /// <summary>Firma un access token JWT y devuelve el token y su vigencia en segundos.</summary>
    (string Token, int ExpiresIn) GenerateAccessToken(User user);

    /// <summary>Genera un refresh token opaco con aleatoriedad criptográfica.</summary>
    string GenerateRefreshToken();

    /// <summary>Hash SHA-256 estable de un refresh token, usado como clave de búsqueda y almacenamiento.</summary>
    string HashRefreshToken(string refreshToken);
}
