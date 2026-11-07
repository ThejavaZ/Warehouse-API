using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WMS.Application.Inventory;
using WMS.Domain.Entities;
using WMS.Infrastructure.Persistence;
using WMS.Infrastructure.Persistence.Repositories;

namespace WMS.Infrastructure.Test;

/// <summary>
/// Pruebas del repositorio de inventario contra SQLite en memoria: verifican la semántica
/// de los UPDATE condicionales (entrada/salida) que son la garantía de concurrencia:
/// dos salidas simultáneas no pueden dejar saldo negativo.
/// </summary>
[TestFixture]
public class InventoryRepositoryTests
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private InventoryRepository _repository = null!;

    private int _warehouseId;
    private int _productId;

    [SetUp]
    public async Task SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new InventoryRepository(_context);

        var warehouse = new Warehouse
        {
            UUID = Guid.NewGuid(),
            Name = "Central",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            UUID = Guid.NewGuid(),
            SKU = "SKU-001",
            Name = "Martillo",
            Price = 12.99m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Warehouses.Add(warehouse);
        _context.Products.Add(product);
        await _context.SaveChangesAsync(CancellationToken.None);

        _warehouseId = warehouse.Id;
        _productId = product.Id;
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task TryIncrease_OnMissingPair_ReturnsZero()
    {
        var affected = await _repository.TryIncreaseQuantityAsync(
            _warehouseId, _productId, 5, CancellationToken.None);

        Assert.That(affected, Is.Zero, "Sin ficha no hay filas que actualizar.");
    }

    [Test]
    public async Task AddAndIncrease_CreatesItem_WithInitialQuantity()
    {
        var created = await _repository.AddAndIncreaseAsync(
            _warehouseId, _productId, 7, CancellationToken.None);

        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(created, Is.True);
            Assert.That(item!.Quantity, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task AddAndIncrease_WhenPairAlreadyExists_IncreasesInsteadOfDuplicating()
    {
        // Simula la carrera del upsert: otro request creó la ficha primero.
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 3, CancellationToken.None);

        var applied = await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 4, CancellationToken.None);

        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(applied, Is.True);
            Assert.That(item!.Quantity, Is.EqualTo(7), "El reinterto debe sumar sobre la ficha ganadora.");
            Assert.That(_context.InventoryItems.Count(), Is.EqualTo(1), "Nunca debe haber dos fichas por par.");
        });
    }

    [Test]
    public async Task TryIncrease_ExistingPair_AddsQuantity()
    {
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 10, CancellationToken.None);

        var affected = await _repository.TryIncreaseQuantityAsync(_warehouseId, _productId, 5, CancellationToken.None);
        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(affected, Is.EqualTo(1));
            Assert.That(item!.Quantity, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task TryDecrease_WithEnoughStock_ReducesQuantity()
    {
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 10, CancellationToken.None);

        var affected = await _repository.TryDecreaseQuantityAsync(_warehouseId, _productId, 6, CancellationToken.None);
        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(affected, Is.EqualTo(1));
            Assert.That(item!.Quantity, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task TryDecrease_WithInsufficientStock_ReturnsZero_AndKeepsQuantity()
    {
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 3, CancellationToken.None);

        // Esta condición Quantity >= cantidad es exactamente lo que impide que dos
        // consumos simultáneos dejen saldo negativo.
        var affected = await _repository.TryDecreaseQuantityAsync(_warehouseId, _productId, 10, CancellationToken.None);
        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(affected, Is.Zero, "Sin saldo suficiente el UPDATE no afecta ninguna fila.");
            Assert.That(item!.Quantity, Is.EqualTo(3), "El saldo no cambia cuando el consumo se rechaza.");
        });
    }

    [Test]
    public async Task TryDecrease_OnMissingPair_ReturnsZero()
    {
        var affected = await _repository.TryDecreaseQuantityAsync(
            _warehouseId, _productId, 1, CancellationToken.None);

        Assert.That(affected, Is.Zero);
    }

    [Test]
    public async Task TryDecrease_CanLeaveZero_ButNeverNegative()
    {
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 5, CancellationToken.None);

        await _repository.TryDecreaseQuantityAsync(_warehouseId, _productId, 5, CancellationToken.None);
        var second = await _repository.TryDecreaseQuantityAsync(_warehouseId, _productId, 1, CancellationToken.None);

        var item = await _repository.GetItemAsync(_warehouseId, _productId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Zero, "Con saldo 0 ya no se puede consumir nada.");
            Assert.That(item!.Quantity, Is.Zero);
        });
    }

    [Test]
    public async Task UniquePair_RejectsDuplicateFichas_AtDatabaseLevel()
    {
        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 1, CancellationToken.None);

        // Insert directo para verificar el índice único (WarehouseId, ProductId) de la BD.
        _context.InventoryItems.Add(new InventoryItem
        {
            WarehouseId = _warehouseId,
            ProductId = _productId,
            Quantity = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        Assert.ThrowsAsync<DbUpdateException>(
            () => _context.SaveChangesAsync(CancellationToken.None));
    }

    [Test]
    public async Task GetAllAsync_FiltersByWarehouseAndProduct_AndProjectsNames()
    {
        var otherProduct = new Product
        {
            UUID = Guid.NewGuid(),
            SKU = "SKU-002",
            Name = "Destornillador",
            Price = 5.0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Products.Add(otherProduct);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 10, CancellationToken.None);
        await _repository.AddAndIncreaseAsync(_warehouseId, otherProduct.Id, 2, CancellationToken.None);

        var (items, total) = await _repository.GetAllAsync(
            new InventoryQuery { WarehouseId = _warehouseId },
            CancellationToken.None);

        var list = items.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(2));
            Assert.That(list.Select(i => i.ProductSku), Is.EquivalentTo(new[] { "SKU-001", "SKU-002" }));
            Assert.That(list.Select(i => i.WarehouseName), Is.All.EqualTo("Central"),
                "La proyección debe traer los nombres resueltos en la misma consulta.");
        });
    }

    [Test]
    public async Task GetAllAsync_CountsAfterFilters()
    {
        var otherProduct = new Product
        {
            UUID = Guid.NewGuid(),
            SKU = "SKU-003",
            Name = "Otros",
            Price = 1.0m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Products.Add(otherProduct);
        await _context.SaveChangesAsync(CancellationToken.None);

        await _repository.AddAndIncreaseAsync(_warehouseId, _productId, 1, CancellationToken.None);
        await _repository.AddAndIncreaseAsync(_warehouseId, otherProduct.Id, 1, CancellationToken.None);

        var (_, total) = await _repository.GetAllAsync(
            new InventoryQuery { ProductId = _productId },
            CancellationToken.None);

        Assert.That(total, Is.EqualTo(1));
    }
}
