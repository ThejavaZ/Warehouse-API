using System.Net;
using System.Net.Http.Json;
using Api.Common;
using Api.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Test.Integration;

[TestFixture]
public class ProductApiTests : IntegrationTestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await ClearDatabaseAsync();
        await AuthenticateAsync();
    }

    [Test]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/Product");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Post_Returns201_WithServerFields()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "SKU-001", Name = "Martillo", Description = "Cabeza de acero", Price = 12.99m },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location, Is.Not.Null);

        var created = await ReadJsonAsync(response);

        Assert.Multiple(() =>
        {
            Assert.That(created.GetProperty("id").GetInt32(), Is.GreaterThan(0));
            Assert.That(created.GetProperty("sku").GetString(), Is.EqualTo("SKU-001"));
            Assert.That(created.GetProperty("price").GetDecimal(), Is.EqualTo(12.99m));
            Assert.That(created.GetProperty("isActive").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Post_DuplicateSku_Returns400()
    {
        await CreateProductAsync("DUP-01");

        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "DUP-01", Name = "Otro", Price = 5m },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("validation_error"));
    }

    [Test]
    public async Task Post_InvalidPayload_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "X", Name = "A", Price = -1m },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.That(problem!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task Get_WithId_ReturnsDetail()
    {
        var created = await CreateProductAsync("DET-01", "Sierra");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.GetAsync($"/api/Product?id={id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>(JsonOptions);

        Assert.That(product!.Name, Is.EqualTo("Sierra"));
    }

    [Test]
    public async Task Get_WithSearchAndIsActive_CombinesFilters()
    {
        await CreateProductAsync("MART-01", "Martillo");
        await CreateProductAsync("DEST-01", "Destornillador");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
            db.Products.First(p => p.SKU == "DEST-01").IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/Product?search=mart&isActive=true");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListViewModel>>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(result!.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().SKU, Is.EqualTo("MART-01"));
        });
    }

    [Test]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/Product/9999",
            new { SKU = "GHOST-01", Name = "Ghost", Price = 1m },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Put_ExistingProduct_UpdatesAndReturns200()
    {
        var created = await CreateProductAsync("UPD-01", "Original");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync(
            $"/api/Product/{id}",
            new { SKU = "UPD-01", Name = "Renombrado", Description = "Nueva", Price = 20.5m },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var updated = await response.Content.ReadFromJsonAsync<ProductViewModel>(JsonOptions);

        Assert.Multiple(() =>
        {
            Assert.That(updated!.Name, Is.EqualTo("Renombrado"));
            Assert.That(updated.Price, Is.EqualTo(20.5m));
        });
    }

    [Test]
    public async Task Delete_ThenGetList_ExcludesSoftDeleted_AndSkuBecomesReusable()
    {
        var created = await CreateProductAsync("DEL-01");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await Client.DeleteAsync($"/api/Product/{id}");
        Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var listResponse = await Client.GetAsync("/api/Product");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<ProductListViewModel>>(JsonOptions);

        Assert.That(result!.TotalItems, Is.Zero, "El producto borrado lógicamente no debe aparecer.");

        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "DEL-01", Name = "Reutilizado", Price = 1m },
            JsonOptions);

        Assert.That(reuseResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            "El índice único filtrado permite reutilizar el SKU de un producto dado de baja.");
    }

    [Test]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await Client.DeleteAsync("/api/Product/9999");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
