using System.Net;
using System.Net.Http.Json;
using Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WMS.Application.Users;
using WMS.Domain.Entities;

namespace WMS.Api.Test;

public class AuthApiTests : IntegrationTestBase, IClassFixture<WarehouseApiFactory>
{
    public AuthApiTests(WarehouseApiFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await ClearDatabaseAsync();
    }

    [Fact]
    public async Task Register_Returns201_WithoutPasswordInBody()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/register",
            new { Name = "Nuevo", Email = "nuevo@test.com", Password = "Secret123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret123!", body);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await RegisterUserAsync("dup@test.com");

        var response = await Client.PostAsJsonAsync(
            "/api/Auth/register",
            new { Name = "Otro", Email = "dup@test.com", Password = "Secret123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        await RegisterUserAsync("user@test.com");

        var login = await LoginAsync("user@test.com");

        Assert.NotNull(login.GetProperty("accessToken").GetString());

        Assert.NotEmpty(login.GetProperty("accessToken").GetString()!);
        Assert.NotNull(login.GetProperty("refreshToken").GetString());
        Assert.NotEmpty(login.GetProperty("refreshToken").GetString()!);
        Assert.True(login.GetProperty("expiresIn").GetInt32() > 0);
        Assert.Equal("Bearer", login.GetProperty("tokenType").GetString());
        Assert.False(login.TryGetProperty("password", out _), "Nunca debe devolverse la contraseña.");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401ProblemDetails()
    {
        await RegisterUserAsync("user@test.com");

        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = "user@test.com", Password = "WrongPass123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("invalid_credentials", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = "ghost@test.com", Password = "Whatever123!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
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

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        await AuthenticateAsync("me@test.com");

        var response = await Client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var user = await response.Content.ReadFromJsonAsync<UserViewModel>(JsonOptions);

        Assert.Equal("me@test.com", user!.Email);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Refresh_ReturnsNewTokens_AndOldRefreshBecomesUnusable()
    {
        var login = await AuthenticateAsync("rotate@test.com");
        var oldRefreshToken = login.GetProperty("refreshToken").GetString();

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = oldRefreshToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        // Reutilizar el token rotado debe fallar: así se detecta un refresh token robado.
        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = oldRefreshToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
    }

    [Fact]
    public async Task Revoke_ThenRefresh_Returns401()
    {
        var login = await AuthenticateAsync("logout@test.com");
        var refreshToken = login.GetProperty("refreshToken").GetString();

        var revokeResponse = await Client.PostAsJsonAsync(
            "/api/Auth/revoke",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401()
    {
        await AuthenticateAsync("tamper@test.com");

        // Se corrompe el último bloque del JWT: la firma deja de validar.
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var tampered = token[..^4] + "AAAA";

        Client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tampered);

        var response = await Client.GetAsync("/api/User");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangingPassword_RevokesUsersRefreshTokens()
    {
        var login = await AuthenticateAsync("cambio@test.com");
        var refreshToken = login.GetProperty("refreshToken").GetString();
        var userId = login.GetProperty("userId").GetInt32();

        var changePassword = await Client.PutAsJsonAsync(
            $"/api/User/{userId}",
            new { Name = "Test User", Email = "cambio@test.com", Password = "NuevaClave456!" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, changePassword.StatusCode);

        var refreshResponse = await Client.PostAsJsonAsync(
            "/api/Auth/refresh",
            new { RefreshToken = refreshToken },
            JsonOptions);

        Assert.True(refreshResponse.StatusCode == HttpStatusCode.Unauthorized,
            "Cambiar la contraseña debe cerrar las sesiones abiertas.");
    }
}
