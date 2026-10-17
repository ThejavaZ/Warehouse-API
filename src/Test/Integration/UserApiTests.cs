using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Common;
using Api.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Test.Integration;

[TestFixture]
public class UserApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private WarehouseApiFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WarehouseApiFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [SetUp]
    public async Task SetUp()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }

    private static UserCreateDtoPayload NewPayload(
        string email = "test@test.com",
        string name = "Test User")
        => new(name, email, "Secret123!");

    private sealed record UserCreateDtoPayload(string Name, string Email, string Password);

    private async Task<JsonElement> CreateUserAsync(string email, string name = "Test User")
    {
        var response = await _client.PostAsJsonAsync("/api/User", NewPayload(email, name), JsonOptions);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private async Task SeedInactiveUserAsync(string email, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();

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

    [Test]
    public async Task Get_WithoutUsers_ReturnsEmptyPagedResult()
    {
        var response = await _client.GetAsync("/api/User");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalItems, Is.Zero);
            Assert.That(result.TotalPages, Is.Zero);
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    public async Task Get_WithSearchAndIsActive_CombinesFilters()
    {
        await CreateUserAsync("ana@test.com", "Ana Garcia");
        await CreateUserAsync("luis@test.com", "Luis Perez");

        // La API no expone IsActive en creación, así que sembramos el usuario inactivo directo en la BD.
        await SeedInactiveUserAsync("ana.baja@test.com", "Ana Baja");

        var response = await _client.GetAsync("/api/User?search=ana&isActive=true");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Name, Is.EqualTo("Ana Garcia"));
        });
    }

    [Test]
    public async Task Get_WithInvalidPageSize_Returns400ProblemDetails()
    {
        var response = await _client.GetAsync("/api/User?pageSize=0");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.That(problem!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task Get_WithUnknownId_Returns404ProblemDetails()
    {
        var response = await _client.GetAsync("/api/User?id=9999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(problem!.Detail, Does.Contain("9999"));
            Assert.That(problem.Extensions["code"]?.ToString(), Is.EqualTo("not_found"));
        });
    }

    [Test]
    public async Task Get_PaginatesAndReturnsPagedMetadata()
    {
        for (int i = 1; i <= 3; i++)
        {
            await CreateUserAsync($"user{i}@test.com", $"User {i}");
        }

        var response = await _client.GetAsync("/api/User?page=2&pageSize=2&orderDesc=true");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalItems, Is.EqualTo(3));
            Assert.That(result.TotalPages, Is.EqualTo(2));
            Assert.That(result.Page, Is.EqualTo(2));
            Assert.That(result.Items.Single().Name, Is.EqualTo("User 1"));
            Assert.That(result.HasPreviousPage, Is.True);
            Assert.That(result.HasNextPage, Is.False);
        });
    }

    [Test]
    public async Task Post_ValidUser_Returns201_WithoutPasswordInBody()
    {
        var response = await _client.PostAsJsonAsync("/api/User", NewPayload(), JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain("password").IgnoreCase);
            Assert.That(body, Does.Not.Contain("Secret123!"));
        });
    }

    [Test]
    public async Task Post_InvalidPayload_Returns400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/User",
            new { Name = "A", Email = "not-an-email", Password = "123" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Post_DuplicateEmail_Returns400()
    {
        await CreateUserAsync("dup@test.com");

        var response = await _client.PostAsJsonAsync("/api/User", NewPayload("dup@test.com"), JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("validation_error"));
    }

    [Test]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/User/9999",
            new { Name = "Ghost", Email = "ghost@test.com" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Put_ExistingUser_UpdatesAndReturns200()
    {
        var created = await CreateUserAsync("update@test.com", "Original Name");
        var id = created.GetProperty("id").GetInt32();

        var response = await _client.PutAsJsonAsync(
            $"/api/User/{id}",
            new { Name = "Updated Name", Email = "update@test.com" },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<UserViewModel>(JsonOptions);

        Assert.That(updated!.Name, Is.EqualTo("Updated Name"));
    }

    [Test]
    public async Task Delete_ThenGetList_ExcludesSoftDeletedUser()
    {
        var created = await CreateUserAsync("delete@test.com");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await _client.DeleteAsync($"/api/User/{id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var listResponse = await _client.GetAsync("/api/User");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<UserListViewModel>>(JsonOptions);

        Assert.That(result!.TotalItems, Is.Zero, "El usuario borrado lógicamente no debe aparecer en el listado.");

        var deleteAgain = await _client.DeleteAsync($"/api/User/{id}");
        Assert.That(deleteAgain.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Post_WithEmailOfSoftDeletedUser_Succeeds()
    {
        var created = await CreateUserAsync("reuse@test.com");
        var id = created.GetProperty("id").GetInt32();

        await _client.DeleteAsync($"/api/User/{id}");

        var response = await _client.PostAsJsonAsync("/api/User", NewPayload("reuse@test.com"), JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            "El índice único filtrado permite reutilizar correos de usuarios dados de baja.");
    }
}
