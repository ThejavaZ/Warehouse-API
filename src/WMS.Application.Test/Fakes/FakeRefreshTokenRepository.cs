using WMS.Application.Auth;
using WMS.Domain.Entities;

namespace WMS.Application.Test.Fakes;

/// <summary>
/// Implementación en memoria de IRefreshTokenRepository para pruebas unitarias del servicio.
/// </summary>
public class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = new();

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        refreshToken.Id = Tokens.Count == 0 ? 1 : Tokens.Max(t => t.Id) + 1;
        Tokens.Add(refreshToken);

        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => Task.FromResult(Tokens.SingleOrDefault(t => t.Token == tokenHash));

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        var index = Tokens.FindIndex(t => t.Id == refreshToken.Id);

        if (index >= 0)
        {
            Tokens[index] = refreshToken;
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(t => t.UserId == userId && t.RevokedAt == null))
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task<int> DeleteExpiredForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var expired = Tokens
            .Where(t => t.UserId == userId && t.ExpiresAt < DateTime.UtcNow)
            .ToList();

        Tokens.RemoveAll(t => expired.Contains(t));

        return Task.FromResult(expired.Count);
    }
}
