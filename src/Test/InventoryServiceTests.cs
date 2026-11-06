using Api.Inventory;
using Api.Products;
using Api.Test.Fakes;
using Api.Warehouses;
using WMS.Domain.Common;

namespace Api.Test;

[TestFixture]
public class InventoryServiceTests
{
    private FakeInventoryRepository _inventory = null!;
    private FakeWarehouseRepository _warehouses = null!;
    private FakeProductRepository _products = null!;
    private InventoryService _service = null!;

    private Warehouse _warehouse = null!;
    private Product _product = null!;

    [SetUp]
    public void SetUp()
    {
        _inventory = new FakeInventoryRepository();
        _warehouses = new FakeWarehouseRepository();
        _products = new FakeProductRepository();
        _service = new InventoryService(_inventory, _warehouses, _products);

        _warehouse = new Warehouse
        {
            Id = 1,
            UUID = Guid.NewGuid(),
            Name = "Central",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _warehouses.Warehouses.Add(_warehouse);

        _product = new Product
        {
            Id = 1,
            UUID = Guid.NewGuid(),
            SKU = "SKU-001",
            Name = "Martillo",
            Price = 12.99m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _products.Products.Add(_product);
    }

    private static InventoryMovementDTO Movement(int quantity, int warehouseId = 1, int productId = 1)
        => new() { WarehouseId = warehouseId, ProductId = productId, Quantity = quantity };

    [Test]
    public async Task Restock_NewPair_CreatesItemWithQuantity()
    {
        var item = await _service.RestockAsync(Movement(10));

        Assert.Multiple(() =>
        {
            Assert.That(item.Quantity, Is.EqualTo(10));
            Assert.That(item.WarehouseId, Is.EqualTo(1));
            Assert.That(item.ProductId, Is.EqualTo(1));
            Assert.That(_inventory.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Restock_ExistingPair_IncreasesQuantity()
    {
        await _service.RestockAsync(Movement(10));
        var item = await _service.RestockAsync(Movement(5));

        Assert.That(item.Quantity, Is.EqualTo(15), "El restock acumula sobre la ficha existente.");
    }

    [Test]
    public void Restock_UnknownWarehouse_ThrowsKeyNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RestockAsync(Movement(5, warehouseId: 999)));
    }

    [Test]
    public void Restock_UnknownProduct_ThrowsKeyNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RestockAsync(Movement(5, productId: 999)));
    }

    [Test]
    public void Restock_SoftDeletedWarehouse_ThrowsKeyNotFound()
    {
        _warehouse.SoftDelete = DateTime.UtcNow;

        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.RestockAsync(Movement(5)));
    }

    [Test]
    public void Restock_InactiveWarehouse_ThrowsDomain400()
    {
        _warehouse.IsActive = false;

        var ex = Assert.ThrowsAsync<DomainException>(() => _service.RestockAsync(Movement(5)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(400));
            Assert.That(ex.Code, Is.EqualTo("inactive_warehouse"));
        });
    }

    [Test]
    public void Restock_InactiveProduct_ThrowsDomain400()
    {
        _product.IsActive = false;

        var ex = Assert.ThrowsAsync<DomainException>(() => _service.RestockAsync(Movement(5)));

        Assert.That(ex!.Code, Is.EqualTo("inactive_product"));
    }

    [Test]
    public async Task Consume_ExistingPair_ReducesQuantity()
    {
        await _service.RestockAsync(Movement(10));

        var item = await _service.ConsumeAsync(Movement(4));

        Assert.That(item.Quantity, Is.EqualTo(6));
    }

    [Test]
    public async Task Consume_ExactQuantity_LeavesZero()
    {
        await _service.RestockAsync(Movement(7));

        var item = await _service.ConsumeAsync(Movement(7));

        Assert.That(item.Quantity, Is.Zero, "Consumir todo el saldo deja la ficha a 0 (nunca negativo).");
    }

    [Test]
    public async Task Consume_InsufficientStock_ThrowsDomain409_AndKeepsQuantity()
    {
        await _service.RestockAsync(Movement(3));

        var ex = Assert.ThrowsAsync<DomainException>(() => _service.ConsumeAsync(Movement(10)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(409));
            Assert.That(ex.Code, Is.EqualTo("insufficient_stock"));
            Assert.That(_inventory.Items.Single().Quantity, Is.EqualTo(3),
                "Un consumo rechazado no debe tocar el saldo.");
        });
    }

    [Test]
    public void Consume_NoStockRecord_ThrowsDomain404()
    {
        var ex = Assert.ThrowsAsync<DomainException>(() => _service.ConsumeAsync(Movement(1)));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(404));
            Assert.That(ex.Code, Is.EqualTo("no_stock_record"));
        });
    }

    [Test]
    public void Restock_ZeroQuantity_ThrowsDomain400()
    {
        var ex = Assert.ThrowsAsync<DomainException>(() => _service.RestockAsync(Movement(0)));

        Assert.That(ex!.Code, Is.EqualTo("invalid_quantity"));
    }

    [Test]
    public async Task GetAll_FiltersByWarehouseAndProduct()
    {
        var otherWarehouse = new Warehouse
        {
            Id = 2,
            UUID = Guid.NewGuid(),
            Name = "Secundario",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _warehouses.Warehouses.Add(otherWarehouse);

        await _service.RestockAsync(Movement(10));
        await _service.RestockAsync(Movement(5, warehouseId: 2));

        var result = await _service.GetAllAsync(new InventoryQuery { WarehouseId = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Quantity, Is.EqualTo(10));
        });
    }
}
