using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Common;
using Api.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Test.Integration;

[TestFixture]
public class InventoryApiTests : IntegrationTestBase
{
    private int _warehouseId;
    private int _productId;

    [SetUp]
    public async Task SetUp()
    {
        await ClearDatabaseAsync();
        await AuthenticateAsync();

        var warehouse = await CreateWarehouseAsync("Almacén Central", "Madrid");
        var product = await CreateProductAsync("SKU-INV-1", "Martillo");

        _warehouseId = warehouse.GetProperty("id").GetInt32();
        _productId = product.GetProperty("id").GetInt32();
    }

    private object Movement(int quantity)
        => new { WarehouseId = _warehouseId, ProductId = _productId, Quantity = quantity };

    private async Task<JsonElement> PostMovementAsync(string action, int quantity)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/InventoryItem/{action}",
            Movement(quantity),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        return await ReadJsonAsync(response);
    }

    [Test]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/InventoryItem");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Restock_NewItem_CreatesRecordWithQuantity()
    {
        var item = await PostMovementAsync("restock", 10);

        Assert.Multiple(() =>
        {
            Assert.That(item.GetProperty("quantity").GetInt32(), Is.EqualTo(10));
            Assert.That(item.GetProperty("warehouseId").GetInt32(), Is.EqualTo(_warehouseId));
            Assert.That(item.GetProperty("productId").GetInt32(), Is.EqualTo(_productId));
        });
    }

    [Test]
    public async Task Restock_Twice_AccumulatesQuantity()
    {
        await PostMovementAsync("restock", 10);
        var item = await PostMovementAsync("restock", 5);

        Assert.That(item.GetProperty("quantity").GetInt32(), Is.EqualTo(15));
    }

    [Test]
    public async Task Consume_ReducesQuantity()
    {
        await PostMovementAsync("restock", 10);

        var item = await PostMovementAsync("consume", 4);

        Assert.That(item.GetProperty("quantity").GetInt32(), Is.EqualTo(6));
    }

    [Test]
    public async Task Consume_InsufficientStock_Returns409ProblemDetails()
    {
        await PostMovementAsync("restock", 3);

        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/consume",
            Movement(10),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/problem+json"));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("insufficient_stock"));

        // El saldo no cambia cuando el consumo se rechaza.
        var listResponse = await Client.GetAsync(
            $"/api/InventoryItem?warehouseId={_warehouseId}&productId={_productId}");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<InventoryItemViewModel>>(JsonOptions);

        Assert.That(result!.Items.Single().Quantity, Is.EqualTo(3));
    }

    [Test]
    public async Task Consume_WithoutStockRecord_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/consume",
            Movement(1),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("no_stock_record"));
    }

    [Test]
    public async Task Restock_UnknownWarehouse_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            new { WarehouseId = 9999, ProductId = _productId, Quantity = 1 },
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Restock_ZeroQuantity_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            Movement(0),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.That(problem!.Errors, Is.Not.Empty);
    }

    [Test]
    public async Task Get_FiltersByWarehouseAndProduct_AndResolvesNames()
    {
        await PostMovementAsync("restock", 10);

        var response = await Client.GetAsync(
            $"/api/InventoryItem?warehouseId={_warehouseId}&productId={_productId}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var result = await response.Content.ReadFromJsonAsync<PagedResult<InventoryItemViewModel>>(JsonOptions);
        var item = result!.Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(item.Quantity, Is.EqualTo(10));
            Assert.That(item.WarehouseName, Is.EqualTo("Almacén Central"));
            Assert.That(item.ProductSku, Is.EqualTo("SKU-INV-1"));
            Assert.That(item.ProductName, Is.EqualTo("Martillo"));
        });
    }

    [Test]
    public async Task Movement_OnInactiveWarehouse_Returns400()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
            db.Warehouses.Single().IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            Movement(1),
            JsonOptions);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.That(problem!.Extensions["code"]?.ToString(), Is.EqualTo("inactive_warehouse"));
    }
}
