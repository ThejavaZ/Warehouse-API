using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using WMS.Infrastructure.Persistence;

namespace WMS.Api.Test;

/// <summary>
/// Base de las pruebas de integración (xUnit). El host de pruebas entra como fixture de
/// clase —una sola instancia por clase de test— y cada prueba recibe un <c>HttpClient</c>
/// nuevo, sin Bearer, con la base limpia y la sesión montada en <c>InitializeAsync</c>.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected WarehouseApiFactory Factory { get; }
    protected HttpClient Client { get; }

    protected IntegrationTestBase(WarehouseApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Equivalente al <c>[SetUp]</c> de NUnit: xUnit lo ejecuta antes de cada prueba.
    /// El cliente es nuevo en cada prueba, así que nunca arrastra el Bearer de la anterior.
    /// </summary>
    public virtual Task InitializeAsync()
    {
        Client.DefaultRequestHeaders.Authorization = null;
        return Task.CompletedTask;
    }

    /// <summary>Libera el cliente de esta prueba (xUnit lo ejecuta tras cada prueba).</summary>
    public virtual Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    protected async Task<JsonElement> CreateWarehouseAsync(string name, string? location = null)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Warehouse",
            new { Name = name, Location = location },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    protected static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }
}
