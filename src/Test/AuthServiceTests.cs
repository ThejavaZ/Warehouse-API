using Api.Auth;
using Api.Security;
using Api.Test.Fakes;
using Microsoft.Extensions.Options;
using WMS.Application.Users;
using WMS.Domain.Common;
using WMS.Domain.Entities;

namespace Api.Test;

[TestFixture]
public class AuthServiceTests
{
    private const string TestSecret = "unit-test-secret-with-at-least-32-characters";

    private FakeUserRepository _users = null!;
    private FakeRefreshTokenRepository _refreshTokens = null!;
    private BCryptPasswordHasher _hasher = null!;
    private UserService _userService = null!;
    private AuthOptions _options = null!;
    private AuthService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _users = new FakeUserRepository();
        _refreshTokens = new FakeRefreshTokenRepository();
        _hasher = new BCryptPasswordHasher();
        _userService = new UserService(_users, _hasher, _refreshTokens);

        _options = new AuthOptions
        {
            Secret = TestSecret,
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7,
            RegistrationEnabled = true
        };

        _service = CreateService(_options);
    }

    private AuthService CreateService(AuthOptions options)
    {
        var tokenService = new JwtTokenService(Options.Create(options));

        return new AuthService(
            _users,
            _userService,
            _hasher,
            _refreshTokens,
            tokenService,
            Options.Create(options));
    }

    private async Task<AuthViewModel> RegisterAndLoginAsync(string email = "user@test.com")
    {
        await _service.RegisterAsync(new UserCreateDTO
        {
            Name = "Test User",
            Email = email,
            Password = "Secret123!"
        });

        return await _service.LoginAsync(new LoginDTO { Email = email, Password = "Secret123!" });
    }

    [Test]
    public async Task RegisterAsync_CreatesUser_AndReturnsViewModel()
    {
        var viewModel = await _service.RegisterAsync(new UserCreateDTO
        {
            Name = "Nuevo",
            Email = "nuevo@test.com",
            Password = "Secret123!"
        });

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Id, Is.GreaterThan(0));
            Assert.That(viewModel.Email, Is.EqualTo("nuevo@test.com"));
            Assert.That(_users.Users.Single().Password, Does.StartWith("$2"), "La contraseña se guarda hasheada.");
        });
    }

    [Test]
    public void RegisterAsync_WhenDisabled_ThrowsDomainException403()
    {
        _options.RegistrationEnabled = false;

        var ex = Assert.ThrowsAsync<DomainException>(
            () => _service.RegisterAsync(new UserCreateDTO { Name = "X", Email = "x@test.com", Password = "Secret123!" }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(403));
            Assert.That(ex.Code, Is.EqualTo("registration_disabled"));
        });
    }

    [Test]
    public async Task LoginAsync_ValidCredentials_ReturnsTokens()
    {
        var auth = await RegisterAndLoginAsync();

        Assert.Multiple(() =>
        {
            Assert.That(auth.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(auth.RefreshToken, Is.Not.Null.And.Not.Empty);
            Assert.That(auth.ExpiresIn, Is.GreaterThan(0));
            Assert.That(auth.UserId, Is.GreaterThan(0));
            Assert.That(auth.Email, Is.EqualTo("user@test.com"));
        });

        // En BD solo queda el HASH del refresh token, nunca el valor en claro.
        var stored = _refreshTokens.Tokens.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Token, Is.Not.EqualTo(auth.RefreshToken));
            Assert.That(stored.Token, Has.Length.EqualTo(64), "Debe ser un hash SHA-256 en hexadecimal.");
            Assert.That(stored.IsActive, Is.True);
        });
    }

    [Test]
    public void LoginAsync_WrongPassword_Throws401()
    {
        Assert.DoesNotThrowAsync(() => RegisterAndLoginAsync());

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.LoginAsync(new LoginDTO { Email = "user@test.com", Password = "WrongPass123!" }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void LoginAsync_UnknownEmail_Throws401_WithSameMessage()
    {
        var unknown = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.LoginAsync(new LoginDTO { Email = "ghost@test.com", Password = "Whatever123!" }));

        var known = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.LoginAsync(new LoginDTO { Email = "user@test.com", Password = "Whatever123!" }));

        Assert.Multiple(() =>
        {
            Assert.That(unknown!.StatusCode, Is.EqualTo(401));
            // El mensaje es idéntico para no revelar si el correo existe.
            Assert.That(unknown.Message, Is.EqualTo(known!.Message));
        });
    }

    [Test]
    public async Task LoginAsync_SoftDeletedUser_Throws401()
    {
        await RegisterAndLoginAsync();
        _users.Users.Single().SoftDelete = DateTime.UtcNow;

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.LoginAsync(new LoginDTO { Email = "user@test.com", Password = "Secret123!" }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task LoginAsync_InactiveUser_Throws401()
    {
        await RegisterAndLoginAsync();
        _users.Users.Single().IsActive = false;

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.LoginAsync(new LoginDTO { Email = "user@test.com", Password = "Secret123!" }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task RefreshAsync_ValidToken_RotatesAndKeepsSession()
    {
        var auth = await RegisterAndLoginAsync();

        var renewed = await _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken });

        var oldStored = _refreshTokens.Tokens.Single(t => t.RevokedAt != null);
        var newStored = _refreshTokens.Tokens.Single(t => t.RevokedAt == null);

        Assert.Multiple(() =>
        {
            Assert.That(renewed.AccessToken, Is.Not.Null.And.Not.Empty);
            Assert.That(renewed.RefreshToken, Is.Not.EqualTo(auth.RefreshToken), "Cada refresh entrega un token nuevo.");
            Assert.That(oldStored.ReplacedByToken, Is.Not.Null.And.Not.Empty);
            Assert.That(newStored.IsActive, Is.True);
        });
    }

    [Test]
    public async Task RefreshAsync_ReusingOldToken_Throws401()
    {
        var auth = await RegisterAndLoginAsync();
        await _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken });

        // Reutilizar el token ya rotado debe fallar: detecta robo/reutilización.
        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void RefreshAsync_UnknownToken_Throws401()
    {
        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = "not-a-real-token" }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task RefreshAsync_ExpiredToken_Throws401()
    {
        var auth = await RegisterAndLoginAsync();
        _refreshTokens.Tokens.Single().ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task RefreshAsync_DeletedUser_Throws401()
    {
        var auth = await RegisterAndLoginAsync();
        await _userService.DeleteUserAsync(auth.UserId);

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401),
            "La baja del usuario revoca sus refresh tokens.");
    }

    [Test]
    public async Task RevokeAsync_ValidToken_ThenRefreshFails()
    {
        var auth = await RegisterAndLoginAsync();

        await _service.RevokeAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken });

        var ex = Assert.ThrowsAsync<WMS.Domain.Common.DomainException>(
            () => _service.RefreshAsync(new RefreshTokenDTO { RefreshToken = auth.RefreshToken }));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public void RevokeAsync_UnknownToken_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(
            () => _service.RevokeAsync(new RefreshTokenDTO { RefreshToken = "unknown" }));
    }

    [Test]
    public async Task LoginAsync_DeletesExpiredTokensOfTheUser()
    {
        var auth = await RegisterAndLoginAsync();

        // Se siembra un token caducado y se comprueba que el próximo login lo limpia.
        _refreshTokens.Tokens.Add(new RefreshToken
        {
            Id = 99,
            Token = "expired-hash",
            UserId = auth.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow.AddDays(-8)
        });

        await _service.LoginAsync(new LoginDTO { Email = "user@test.com", Password = "Secret123!" });

        Assert.That(_refreshTokens.Tokens.Any(t => t.Token == "expired-hash"), Is.False,
            "El login limpia oportunamente los refresh tokens caducados.");
    }
}
