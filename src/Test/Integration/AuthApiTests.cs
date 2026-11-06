using System.Net;
using System.Net.Http.Json;
using Api.Auth;
using Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Test.Integration;

[TestFixture]
public class AuthApiTests : IntegrationTestBase
{
    [SetUp]
    public Task SetUp() => ClearDatabaseAsync();

    [Test]
    public async Task Register_Returns201_WithoutPasswordInBody()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/register",
            new { Name = "Nuevo", Email = "nuevo@test.com", Password = "Secret123!" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain("password").IgnoreCase);
            Assert.That(body, Does.Not.Contain("Secret123!"));
        });
    }

    [Test]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await RegisterUserAsync("dup@test.com");

        var response = await Client.PostAsJsonAsync(
            "/api/Auth/register",
            new { Name = "Otro", Email = "dup@test.com", Password = "Secret123!" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        await RegisterUserAsync("user@test.com");

        var login = await LoginAsync("user@test.com");

        Assert.Multiple(() =>
        {
            Assert.That(login.GetProperty("accessToken").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(login.GetProperty("refreshToken").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(login.GetProperty("expiresIn").GetInt32(), Is.GreaterThan(0));
            Assert.That(login.GetProperty("tokenType").GetString(), Is.EqualTo("Bearer"));
            Assert.That(login.TryGetProperty("password", out _), Is.False, "Nunca debe devolverse la contraseña.");
        });
    }

    [Test]
    public async Task Login_WrongPassword_Returns401ProblemDetails()
    {
        await RegisterUserAsync("user@test.com");

        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = "user@test.com", Password = "WrongPass123!" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("invalid_credentials"));
    }

    [Test]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = "ghost@test.com", Password = "Whatever123!" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_SoftDeletedUser_Returns401()
    {
        await RegisterUserAsync("baja@test.com");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WMS.Infrastructure.Persistence.AppDbContext>();
            db.Users.Single().SoftDelete = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = "baja@test.com", Password = "Secret123!" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        await AuthenticateAsync("me@test.com");

        var response = await Client.GetAsync("/api/Auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var user = await response.Content.ReadFromJsonAsync<Api.Users.UserViewModel>(JsonOptions);

        Assert.That(user!.Email, Is.EqualTo("me@test.com"));
    }

    [Test]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/Auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task Refresh_ReturnsNewTokens_AndOldRefreshBecomesUnusable()
    {
        var login = await AuthenticateAsync("rotate@test.com");
        var oldRefreshToken = login.GetProperty("refreshToken").GetString();

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = oldRefreshToken },
            JsonOptions);

        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        // Reutilizar el token rotado debe fallar: así se detecta un refresh token robado.
        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = oldRefreshToken },
            JsonOptions);

        Assert.That(reuseResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Revoke_ThenRefresh_Returns401()
    {
        var login = await AuthenticateAsync("logout@test.com");
        var refreshToken = login.GetProperty("refreshToken").GetString();

        var revokeResponse = await Client.PostAsJsonAsync(
            "/api/Auth/revoke",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.That(revokeResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401()
    {
        await AuthenticateAsync("tamper@test.com");

        // Se corrompe el último bloque del JWT: la firma deja de validar.
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var tampered = token[..^4] + "AAAA";

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tampered);

        var response = await Client.GetAsync("/api/User");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ChangingPassword_RevokesUsersRefreshTokens()
    {
        var login = await AuthenticateAsync("cambio@test.com");
        var refreshToken = login.GetProperty("refreshToken").GetString();
        var userId = login.GetProperty("userId").GetInt32();

        var changePassword = await Client.PutAsJsonAsync(
            $"/api/User/{userId}",
            new { Name = "Test User", Email = "cambio@test.com", Password = "NuevaClave456!" },
            JsonOptions);

        Assert.That(changePassword.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.That(refreshResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
            "Cambiar la contraseña debe cerrar las sesiones abiertas.");
    }
}
