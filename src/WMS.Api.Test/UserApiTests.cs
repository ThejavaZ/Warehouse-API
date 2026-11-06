using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WMS.Application.Common;
using WMS.Application.Users;
using WMS.Domain.Entities;

namespace WMS.Api.Test;

public class UserApiTests : IntegrationTestBase, IClassFixture<WarehouseApiFactory>
{
    public UserApiTests(WarehouseApiFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        // El access token es un JWT stateless (no consulta la BD al validar), así que se
        // autentica primero y después se limpia: cada prueba arranca con el Bearer montado
        // y el listado vacío. El correo de sesión es único para que el registro nunca choque
        // con usuarios que haya dejado la prueba anterior.
        await AuthenticateAsync($"session-{Guid.NewGuid():N}@test.com");
        await ClearDatabaseAsync();
    }

    private static UserCreateDtoPayload NewPayload(
        string email = "test@test.com",
        string name = "Test User")
        => new(name, email, "Secret123!");

    private sealed record UserCreateDtoPayload(string Name, string Email, string Password);

    private async Task<JsonElement> CreateUserAsync(string email, string name = "Test User")
    {
        var response = await Client.PostAsJsonAsync("/api/User", NewPayload(email, name), JsonOptions);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    private async Task SeedInactiveUserAsync(string email, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WMS.Infrastructure.Persistence.AppDbContext>();

        db.Users.Add(new User
        {
            UUID = Guid.NewGuid(),
            Name = name,
            Email = email,
            Password = "hashed-placeholder",
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401ProblemDetails()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/User");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("unauthorized", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Get_WithoutUsers_ReturnsEmptyPagedResult()
    {
        var response = await Client.GetAsync("/api/User");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Equal(0, result!.TotalItems);
        Assert.Equal(0, result.TotalPages);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Get_WithSearchAndIsActive_CombinesFilters()
    {
        await CreateUserAsync("ana@test.com", "Ana Garcia");
        await CreateUserAsync("luis@test.com", "Luis Perez");

        // La API no expone IsActive en creación, así que sembramos el usuario inactivo directo en la BD.
        await SeedInactiveUserAsync("ana.baja@test.com", "Ana Baja");

        var response = await Client.GetAsync("/api/User?search=ana&isActive=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Equal(1, result!.TotalItems);
        Assert.Equal("Ana Garcia", result.Items.Single().Name);
    }

    [Fact]
    public async Task Get_WithInvalidPageSize_Returns400ProblemDetails()
    {
        var response = await Client.GetAsync("/api/User?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Get_WithUnknownId_Returns404ProblemDetails()
    {
        var response = await Client.GetAsync("/api/User?id=9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Contains("9999", problem!.Detail);
        Assert.Equal("not_found", problem.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Get_PaginatesAndReturnsPagedMetadata()
    {
        for (int i = 1; i <= 3; i++)
        {
            await CreateUserAsync($"user{i}@test.com", $"User {i}");
        }

        var response = await Client.GetAsync("/api/User?page=2&pageSize=2&orderDesc=true");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Equal(3, result!.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal("User 1", result.Items.Single().Name);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public async Task Post_ValidUser_Returns201_WithoutPasswordInBody()
    {
        var response = await Client.PostAsJsonAsync("/api/User", NewPayload(), JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secret123!", body);
    }

    [Fact]
    public async Task Post_InvalidPayload_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/User",
            new { Name = "A", Email = "not-an-email", Password = "123" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_DuplicateEmail_Returns400()
    {
        await CreateUserAsync("dup@test.com");

        var response = await Client.PostAsJsonAsync("/api/User", NewPayload("dup@test.com"), JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("validation_error", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/User/9999",
            new { Name = "Ghost", Email = "ghost@test.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ExistingUser_UpdatesAndReturns200()
    {
        var created = await CreateUserAsync("update@test.com", "Original Name");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync(
            $"/api/User/{id}",
            new { Name = "Updated Name", Email = "update@test.com" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserViewModel>(JsonOptions);

        Assert.Equal("Updated Name", updated!.Name);
    }

    [Fact]
    public async Task Delete_ThenGetList_ExcludesSoftDeletedUser()
    {
        var created = await CreateUserAsync("delete@test.com");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await Client.DeleteAsync($"/api/User/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await Client.GetAsync("/api/User");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.True(result!.TotalItems == 0, "El usuario borrado lógicamente no debe aparecer en el listado.");

        var deleteAgain = await Client.DeleteAsync($"/api/User/{id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteAgain.StatusCode);
    }

    [Fact]
    public async Task Post_WithEmailOfSoftDeletedUser_Succeeds()
    {
        var created = await CreateUserAsync("reuse@test.com");
        var id = created.GetProperty("id").GetInt32();

        await Client.DeleteAsync($"/api/User/{id}");

        var response = await Client.PostAsJsonAsync("/api/User", NewPayload("reuse@test.com"), JsonOptions);

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            "El índice único filtrado permite reutilizar correos de usuarios dados de baja.");
    }
}
