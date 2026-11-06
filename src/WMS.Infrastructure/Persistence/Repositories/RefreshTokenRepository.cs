using Api.Auth;
using Microsoft.EntityFrameworkCore;
using WMS.Infrastructure.Persistence;

namespace WMS.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    // Sin AsNoTracking: el flujo de refresh rota (revoca) el token, así que hay que poder modificarlo.
    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        return await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == tokenHash, cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _context.RefreshTokens.Update(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeAllForUserAsync(int userId, CancellationToken cancellationToken)
    {
        // UPDATE directo sin materializar los tokens.
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<int> DeleteExpiredForUserAsync(int userId, CancellationToken cancellationToken)
    {
        return await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
