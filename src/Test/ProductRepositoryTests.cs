using Api.Products;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WMS.Infrastructure.Persistence;
using WMS.Infrastructure.Persistence.Repositories;

namespace Api.Test;

/// <summary>
/// Pruebas del repositorio de productos contra una base de datos real (SQLite en memoria),
/// para verificar que las consultas IQueryable se traducen correctamente a SQL.
/// </summary>
[TestFixture]
public class ProductRepositoryTests
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private ProductRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new ProductRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<Product> SeedProductAsync(
        string sku,
        string name,
        decimal price = 10.0m,
        bool isActive = true,
        DateTime? softDelete = null)
    {
        var product = new Product
        {
            UUID = Guid.NewGuid(),
            SKU = sku,
            Name = name,
            Price = price,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SoftDelete = softDelete
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync(CancellationToken.None);

        return product;
    }

    [Test]
    public async Task GetAllProductsAsync_SearchMatchesNameOrSku_IgnoringCase()
    {
        await SeedProductAsync("MART-01", "Martillo");
        await SeedProductAsync("DEST-01", "Destornillador");
        await SeedProductAsync("mart-99", "Repuesto Martillo");

        var (products, total) = await _repository.GetAllProductsAsync(
            new ProductQuery { Search = "MART" },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(2));
            Assert.That(products.Select(p => p.Name), Is.EquivalentTo(new[] { "Martillo", "Repuesto Martillo" }));
        });
    }

    [Test]
    public async Task GetAllProductsAsync_ExcludesSoftDeletedProducts()
    {
        await SeedProductAsync("ACT-01", "Activo");
        await SeedProductAsync("DEL-01", "Borrado", softDelete: DateTime.UtcNow);

        var (products, total) = await _repository.GetAllProductsAsync(
            new ProductQuery(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(1));
            Assert.That(products.Single().SKU, Is.EqualTo("ACT-01"));
        });
    }

    [Test]
    public async Task GetAllProductsAsync_CountsAfterFilters_AndPaginates()
    {
        for (int i = 1; i <= 5; i++)
        {
            await SeedProductAsync($"SKU-{i:D3}", $"Producto {i}");
        }

        var (products, total) = await _repository.GetAllProductsAsync(
            new ProductQuery { Page = 2, PageSize = 2, OrderDesc = true },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(5), "El conteo debe aplicarse con los filtros antes del Skip/Take.");
            Assert.That(products.Select(p => p.Name), Is.EqualTo(new[] { "Producto 3", "Producto 2" }));
        });
    }

    [Test]
    public async Task UniqueSku_AllowsReuseAfterSoftDelete()
    {
        await SeedProductAsync("REUSE-01", "Original", softDelete: DateTime.UtcNow);

        Assert.DoesNotThrowAsync(async () =>
        {
            await SeedProductAsync("REUSE-01", "Nuevo producto");
        });
    }

    [Test]
    public void UniqueSku_RejectsActiveDuplicates()
    {
        SeedProductAsync("DUP-01", "Primero").GetAwaiter().GetResult();

        var ex = Assert.Throws<DbUpdateException>(() =>
        {
            SeedProductAsync("DUP-01", "Segundo").GetAwaiter().GetResult();
        });

        Assert.That(ex, Is.Not.Null, "La base de datos debe rechazar SKUs duplicados activos.");
    }

    [Test]
    public async Task Price_IsStoredWithDecimalPrecision()
    {
        await SeedProductAsync("PRICE-01", "Carpeta", price: 19.99m);

        var stored = await _repository.GetProductBySkuAsync("PRICE-01", CancellationToken.None);

        Assert.That(stored!.Price, Is.EqualTo(19.99m), "El precio debe conservar sus decimales (no es double).");
    }

    [Test]
    public async Task GetProductBySkuAsync_ReturnsMatchingProduct()
    {
        await SeedProductAsync("FIND-01", "Encontrado");

        var product = await _repository.GetProductBySkuAsync("FIND-01", CancellationToken.None);

        Assert.That(product, Is.Not.Null);
        Assert.That(product!.Name, Is.EqualTo("Encontrado"));
    }
}
