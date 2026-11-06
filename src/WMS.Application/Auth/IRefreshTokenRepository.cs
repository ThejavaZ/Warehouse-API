using WMS.Domain.Entities;

namespace WMS.Application.Auth;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    /// <summary>Revoca todos los refresh tokens activos de un usuario (cambio de contraseña o baja).</summary>
    Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken);

    /// <summary>Limpia oportunamente los tokens caducados del usuario; devuelve los eliminados.</summary>
    Task<int> DeleteExpiredForUserAsync(int userId, CancellationToken cancellationToken);
}
