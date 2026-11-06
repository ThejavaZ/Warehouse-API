using System.Net;
using System.Net.Http.Json;
using Api.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WMS.Application.Common;

namespace WMS.Api.Test;

public class ProductApiTests : IntegrationTestBase, IClassFixture<WarehouseApiFactory>
{
    public ProductApiTests(WarehouseApiFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await ClearDatabaseAsync();
        await AuthenticateAsync();
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/Product");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns201_WithServerFields()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "SKU-001", Name = "Martillo", Description = "Cabeza de acero", Price = 12.99m },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await ReadJsonAsync(response);

        Assert.True(created.GetProperty("id").GetInt32() > 0);
        Assert.Equal("SKU-001", created.GetProperty("sku").GetString());
        Assert.Equal(12.99m, created.GetProperty("price").GetDecimal());
        Assert.True(created.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Post_DuplicateSku_Returns400()
    {
        await CreateProductAsync("DUP-01");

        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "DUP-01", Name = "Otro", Price = 5m },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("validation_error", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Post_InvalidPayload_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "X", Name = "A", Price = -1m },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Get_WithId_ReturnsDetail()
    {
        var created = await CreateProductAsync("DET-01", "Sierra");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.GetAsync($"/api/Product?id={id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>(JsonOptions);

        Assert.Equal("Sierra", product!.Name);
    }

    [Fact]
    public async Task Get_WithSearchAndIsActive_CombinesFilters()
    {
        await CreateProductAsync("MART-01", "Martillo");
        await CreateProductAsync("DEST-01", "Destornillador");

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WMS.Infrastructure.Persistence.AppDbContext>();
            db.Products.First(p => p.SKU == "DEST-01").IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync("/api/Product?search=mart&isActive=true");

        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProductListViewModel>>(JsonOptions);

        Assert.Equal(1, result!.TotalItems);
        Assert.Equal("MART-01", result.Items.Single().SKU);
    }

    [Fact]
    public async Task Put_UnknownId_Returns404()
    {
        var response = await Client.PutAsJsonAsync(
            "/api/Product/9999",
            new { SKU = "GHOST-01", Name = "Ghost", Price = 1m },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ExistingProduct_UpdatesAndReturns200()
    {
        var created = await CreateProductAsync("UPD-01", "Original");
        var id = created.GetProperty("id").GetInt32();

        var response = await Client.PutAsJsonAsync(
            $"/api/Product/{id}",
            new { SKU = "UPD-01", Name = "Renombrado", Description = "Nueva", Price = 20.5m },
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ProductViewModel>(JsonOptions);

        Assert.Equal("Renombrado", updated!.Name);
        Assert.Equal(20.5m, updated.Price);
    }

    [Fact]
    public async Task Delete_ThenGetList_ExcludesSoftDeleted_AndSkuBecomesReusable()
    {
        var created = await CreateProductAsync("DEL-01");
        var id = created.GetProperty("id").GetInt32();

        var deleteResponse = await Client.DeleteAsync($"/api/Product/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var listResponse = await Client.GetAsync("/api/Product");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<ProductListViewModel>>(JsonOptions);

        Assert.True(result!.TotalItems == 0, "El producto borrado lógicamente no debe aparecer.");

        var reuseResponse = await Client.PostAsJsonAsync(
            "/api/Product",
            new { SKU = "DEL-01", Name = "Reutilizado", Price = 1m },
            JsonOptions);

        Assert.True(reuseResponse.StatusCode == HttpStatusCode.Created,
            "El índice único filtrado permite reutilizar el SKU de un producto dado de baja.");
    }

    [Fact]
    public async Task Delete_UnknownId_Returns404()
    {
        var response = await Client.DeleteAsync("/api/Product/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
