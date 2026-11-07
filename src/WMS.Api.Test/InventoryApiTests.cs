using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using WMS.Application.Common;
using WMS.Application.Inventory;
using WMS.Domain.Entities;

namespace WMS.Api.Test;

public class InventoryApiTests : IntegrationTestBase, IClassFixture<WarehouseApiFactory>
{
    public InventoryApiTests(WarehouseApiFactory factory) : base(factory) { }

    private int _warehouseId;
    private int _productId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await ReadJsonAsync(response);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        Client.DefaultRequestHeaders.Authorization = null;

        var response = await Client.GetAsync("/api/InventoryItem");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Restock_NewItem_CreatesRecordWithQuantity()
    {
        var item = await PostMovementAsync("restock", 10);

        Assert.Equal(10, item.GetProperty("quantity").GetInt32());
        Assert.Equal(_warehouseId, item.GetProperty("warehouseId").GetInt32());
        Assert.Equal(_productId, item.GetProperty("productId").GetInt32());
    }

    [Fact]
    public async Task Restock_Twice_AccumulatesQuantity()
    {
        await PostMovementAsync("restock", 10);
        var item = await PostMovementAsync("restock", 5);

        Assert.Equal(15, item.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Consume_ReducesQuantity()
    {
        await PostMovementAsync("restock", 10);

        var item = await PostMovementAsync("consume", 4);

        Assert.Equal(6, item.GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Consume_InsufficientStock_Returns409ProblemDetails()
    {
        await PostMovementAsync("restock", 3);

        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/consume",
            Movement(10),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("insufficient_stock", problem!.Extensions["code"]?.ToString());

        // El saldo no cambia cuando el consumo se rechaza.
        var listResponse = await Client.GetAsync(
            $"/api/InventoryItem?warehouseId={_warehouseId}&productId={_productId}");
        var result = await listResponse.Content.ReadFromJsonAsync<PagedResult<InventoryItemViewModel>>(JsonOptions);

        Assert.Equal(3, result!.Items.Single().Quantity);
    }

    [Fact]
    public async Task Consume_WithoutStockRecord_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/consume",
            Movement(1),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("no_stock_record", problem!.Extensions["code"]?.ToString());
    }

    [Fact]
    public async Task Restock_UnknownWarehouse_Returns404()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            new { WarehouseId = 9999, ProductId = _productId, Quantity = 1 },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Restock_ZeroQuantity_Returns400()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            Movement(0),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);

        Assert.NotEmpty(problem!.Errors);
    }

    [Fact]
    public async Task Get_FiltersByWarehouseAndProduct_AndResolvesNames()
    {
        await PostMovementAsync("restock", 10);

        var response = await Client.GetAsync(
            $"/api/InventoryItem?warehouseId={_warehouseId}&productId={_productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<InventoryItemViewModel>>(JsonOptions);
        var item = result!.Items.Single();

        Assert.Equal(1, result.TotalItems);
        Assert.Equal(10, item.Quantity);
        Assert.Equal("Almacén Central", item.WarehouseName);
        Assert.Equal("SKU-INV-1", item.ProductSku);
        Assert.Equal("Martillo", item.ProductName);
    }

    [Fact]
    public async Task Movement_OnInactiveWarehouse_Returns400()
    {
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WMS.Infrastructure.Persistence.AppDbContext>();
            db.Warehouses.Single().IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await Client.PostAsJsonAsync(
            "/api/InventoryItem/restock",
            Movement(1),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);

        Assert.Equal("inactive_warehouse", problem!.Extensions["code"]?.ToString());
    }
}
