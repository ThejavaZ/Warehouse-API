using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WMS.Infrastructure.Persistence;

namespace Api.Test.Integration;

/// <summary>
/// Base de las pruebas de integración: arranca la API completa (SQLite en memoria),
/// limpia la base entre pruebas y ofrece helpers para registrar, autenticar y crear recursos.
/// </summary>
public abstract class IntegrationTestBase
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected WarehouseApiFactory Factory = null!;
    protected HttpClient Client = null!;

    [OneTimeSetUp]
    public void BaseOneTimeSetUp()
    {
        Factory = CreateFactory();
        Client = Factory.CreateClient();
    }

    /// <summary>
    /// Cada prueba arranca sin Bearer: los fixtures que necesiten sesión lo montan en su
    /// propio <c>SetUp</c>, así un token de una prueba anterior no contamina a la siguiente.
    /// </summary>
    [SetUp]
    public void BaseSetUp()
    {
        Client.DefaultRequestHeaders.Authorization = null;
    }

    /// <summary>Fábrica del host de pruebas; se puede sobrescribir para diagnósticos.</summary>
    protected virtual WarehouseApiFactory CreateFactory() => new WarehouseApiFactory();

    [OneTimeTearDown]
    public void BaseOneTimeTearDown()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    /// <summary>Limpia las tablas del negocio entre pruebas (en orden por claves ajenas).</summary>
    protected async Task ClearDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.InventoryItems.RemoveRange(db.InventoryItems);
        db.Products.RemoveRange(db.Products);
        db.Warehouses.RemoveRange(db.Warehouses);
        db.Users.RemoveRange(db.Users); // los refresh tokens caen en cascada

        await db.SaveChangesAsync();
    }

    protected async Task<JsonElement> RegisterUserAsync(
        string email,
        string password = "Secret123!",
        string name = "Test User")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/register",
            new { Name = name, Email = email, Password = password },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        return await ReadJsonAsync(response);
    }

    /// <summary>Registra un usuario, inicia sesión y deja el cliente con el Bearer montado.</summary>
    protected async Task<JsonElement> AuthenticateAsync(
        string email = "test@test.com",
        string password = "Secret123!")
    {
        await RegisterUserAsync(email, password);

        var login = await LoginAsync(email, password);
        var accessToken = login.GetProperty("accessToken").GetString();

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return login;
    }

    protected async Task<JsonElement> LoginAsync(string email, string password = "Secret123!")
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Auth/login",
            new { Email = email, Password = password },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return await ReadJsonAsync(response);
    }

    protected async Task<JsonElement> CreateProductAsync(
        string sku,
        string name = "Test Product",
        decimal price = 10.5m)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = sku, Name = name, Price = price },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        return await ReadJsonAsync(response);
    }

    protected async Task<JsonElement> CreateWarehouseAsync(string name, string? location = null)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = name, Location = location },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        return await ReadJsonAsync(response);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
