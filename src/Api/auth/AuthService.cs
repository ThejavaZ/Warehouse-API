using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using WMS.Application.Auth;
using WMS.Application.Users;
using WMS.Domain.Common;
using WMS.Domain.Entities;

namespace Api.Auth;

public class AuthService : IAuthService
{
    // Hash descartable: permite verificar una contraseña cuando el correo no existe
    // sin que el tiempo de respuesta delate que la cuenta no está registrada.
    private static readonly string DummyPasswordHash =
        BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), workFactor: 11);

    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IJwtTokenService _tokenService;
    private readonly AuthOptions _options;

    public AuthService(
        IUserRepository userRepository,
        IUserService userService,
        IPasswordHasher<User> passwordHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IJwtTokenService tokenService,
        IOptions<AuthOptions> options)
    {
        _userRepository = userRepository;
        _userService = userService;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _options = options.Value;
    }

    public async Task<AuthViewModel> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetUserByEmailAsync(dto.Email, cancellationToken);

        // Cuenta inexistente: se verifica contra el hash descartable para igualar el tiempo de respuesta.
        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user ?? new User(),
            user?.Password ?? DummyPasswordHash,
            dto.Password);

        if (user == null
            || user.SoftDelete != null
            || !user.IsActive
            || verificationResult == PasswordVerificationResult.Failed)
        {
            // Un único mensaje para no revelar si el correo existe o si la contraseña falló.
            throw new DomainException("invalid_credentials", "Email or password is incorrect.", 401);
        }

        // Limpieza oportunista de tokens caducados: evita un job de fondo sin perder nada.
        await _refreshTokenRepository.DeleteExpiredForUserAsync(user.Id, cancellationToken);

        return await IssueTokensAsync(user, previousToken: null, cancellationToken);
    }

    public async Task<AuthViewModel> RefreshAsync(RefreshTokenDTO dto, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashRefreshToken(dto.RefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (stored == null || stored.RevokedAt != null)
        {
            throw new DomainException("invalid_refresh_token", "The refresh token is invalid.", 401);
        }

        if (stored.IsExpired)
        {
            throw new DomainException("refresh_token_expired", "The refresh token has expired.", 401);
        }

        var user = await _userRepository.GetUserByIdAsync(stored.UserId, cancellationToken);

        if (user == null || user.SoftDelete != null || !user.IsActive)
        {
            throw new DomainException("invalid_refresh_token", "The refresh token is invalid.", 401);
        }

        // Rotación: el token usado queda revocado y se guarda el hash del que lo reemplaza,
        // de modo que un refresh token ya usado no pueda volver a canjearse.
        return await IssueTokensAsync(user, stored, cancellationToken);
    }

    public async Task RevokeAsync(RefreshTokenDTO dto, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.HashRefreshToken(dto.RefreshToken);
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        // Responde sin error aunque el token no exista o ya esté revocado:
        // no debe permitirse adivinar tokens válidos por la respuesta.
        if (stored == null || stored.RevokedAt != null)
        {
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.UpdateAsync(stored, cancellationToken);
    }

    public async Task<UserViewModel> RegisterAsync(UserCreateDTO dto, CancellationToken cancellationToken = default)
    {
        if (!_options.RegistrationEnabled)
        {
            throw new DomainException("registration_disabled", "Self-registration is disabled.", 403);
        }

        // Reutiliza las mismas reglas de negocio que el alta desde el módulo Users.
        return await _userService.CreateUserAsync(dto, cancellationToken);
    }

    private async Task<AuthViewModel> IssueTokensAsync(
        User user,
        RefreshToken? previousToken,
        CancellationToken cancellationToken)
    {
        var (accessToken, expiresIn) = _tokenService.GenerateAccessToken(user);

        var refreshTokenValue = _tokenService.GenerateRefreshToken();
        var refreshHash = _tokenService.HashRefreshToken(refreshTokenValue);

        if (previousToken != null)
        {
            previousToken.RevokedAt = DateTime.UtcNow;
            previousToken.ReplacedByToken = refreshHash;
            await _refreshTokenRepository.UpdateAsync(previousToken, cancellationToken);
        }

        var storedToken = new RefreshToken
        {
            Token = refreshHash,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.AddAsync(storedToken, cancellationToken);

        return new AuthViewModel
        {
            AccessToken = accessToken,
            ExpiresIn = expiresIn,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = storedToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email
        };
    }
}
