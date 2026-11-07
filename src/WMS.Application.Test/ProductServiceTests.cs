using WMS.Application.Products;
using WMS.Application.Test.Fakes;
using WMS.Domain.Entities;

namespace WMS.Application.Test;

[TestFixture]
public class ProductServiceTests
{
    private FakeProductRepository _repository = null!;
    private ProductService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeProductRepository();
        _service = new ProductService(_repository);
    }

    private static ProductCreateDTO NewDto(
        string sku = "SKU-001",
        string name = "Martillo",
        decimal price = 12.99m)
        => new() { SKU = sku, Name = name, Price = price };

    private async Task<Product> CreateStoredProductAsync(
        string sku = "SKU-001",
        string name = "Martillo",
        bool isActive = true)
    {
        await _service.CreateProductAsync(NewDto(sku, name));

        var product = _repository.Products.Single(p => p.SKU == sku);
        product.IsActive = isActive;

        return product;
    }

    [Test]
    public async Task CreateProduct_ReturnsViewModel_WithServerFields()
    {
        var viewModel = await _service.CreateProductAsync(NewDto());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Id, Is.GreaterThan(0));
            Assert.That(viewModel.UUID, Is.Not.EqualTo(Guid.Empty));
            Assert.That(viewModel.SKU, Is.EqualTo("SKU-001"));
            Assert.That(viewModel.Name, Is.EqualTo("Martillo"));
            Assert.That(viewModel.Price, Is.EqualTo(12.99m));
            Assert.That(viewModel.IsActive, Is.True);
            Assert.That(viewModel.Description, Is.Null);
        });
    }

    [Test]
    public void CreateProduct_DuplicateSku_ThrowsArgumentException()
    {
        Assert.DoesNotThrowAsync(() => _service.CreateProductAsync(NewDto()));

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.CreateProductAsync(NewDto()));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task CreateProduct_WithSoftDeletedSku_AllowsReuse()
    {
        var existing = await CreateStoredProductAsync("reusable-sku");
        existing.SoftDelete = DateTime.UtcNow;

        Assert.DoesNotThrowAsync(() => _service.CreateProductAsync(NewDto("reusable-sku", "Otro")));
    }

    [Test]
    public void GetProductById_WhenNotFound_ThrowsKeyNotFound()
    {
        var ex = Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetProductByIdAsync(999));

        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task GetProductById_WhenSoftDeleted_ThrowsKeyNotFound()
    {
        var product = await CreateStoredProductAsync();
        product.SoftDelete = DateTime.UtcNow;

        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetProductByIdAsync(product.Id));
    }

    [Test]
    public async Task GetAllProducts_EmptyRepository_ReturnsZeroTotalPages()
    {
        var result = await _service.GetAllProductsAsync(new ProductQuery());

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.Zero);
            Assert.That(result.TotalPages, Is.Zero);
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(500, 100)]
    public async Task GetAllProducts_InvalidPageSize_IsSanitized(int requested, int expected)
    {
        await CreateStoredProductAsync();

        var result = await _service.GetAllProductsAsync(new ProductQuery { PageSize = requested });

        Assert.That(result.PageSize, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetAllProducts_SearchAndIsActive_AreCombined()
    {
        await CreateStoredProductAsync("MART-01", "Martillo");
        await CreateStoredProductAsync("MART-02", "Mármol", isActive: false);
        await CreateStoredProductAsync("DEST-01", "Destornillador");

        var result = await _service.GetAllProductsAsync(
            new ProductQuery { Search = "mart", IsActive = true });

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().SKU, Is.EqualTo("MART-01"));
        });
    }

    [Test]
    public async Task GetAllProducts_Search_MatchesSkuToo()
    {
        await CreateStoredProductAsync("abc-123", "Genérico");

        var result = await _service.GetAllProductsAsync(new ProductQuery { Search = "ABC" });

        Assert.That(result.TotalItems, Is.EqualTo(1), "La búsqueda debe cubrir nombre y SKU.");
    }

    [Test]
    public async Task GetAllProducts_SoftDeletedProducts_AreExcluded()
    {
        var deleted = await CreateStoredProductAsync("DEL-01", "Borrado");
        deleted.SoftDelete = DateTime.UtcNow;
        await CreateStoredProductAsync("OK-01", "Activo");

        var result = await _service.GetAllProductsAsync(new ProductQuery());

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().SKU, Is.EqualTo("OK-01"));
        });
    }

    [Test]
    public void UpdateProduct_WhenNotFound_ThrowsKeyNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateProductAsync(999, new ProductUpdateDTO { SKU = "X-1", Name = "X", Price = 1 }));
    }

    [Test]
    public async Task UpdateProduct_WithAnotherProductsSku_ThrowsArgumentException()
    {
        await CreateStoredProductAsync("owner-sku");
        var target = await CreateStoredProductAsync("target-sku");

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateProductAsync(
            target.Id,
            new ProductUpdateDTO { SKU = "owner-sku", Name = "Target", Price = 5 }));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task UpdateProduct_ChangesPriceAndKeepsDecimalPrecision()
    {
        var target = await CreateStoredProductAsync();

        var updated = await _service.UpdateProductAsync(
            target.Id,
            new ProductUpdateDTO { SKU = target.SKU, Name = "Renombrado", Price = 19.90m });

        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("Renombrado"));
            Assert.That(updated.Price, Is.EqualTo(19.90m), "El precio se maneja como decimal, nunca double.");
        });
    }

    [Test]
    public void DeleteProduct_WhenNotFound_ReturnsFalse()
    {
        Assert.That(_service.DeleteProductAsync(999).Result, Is.False);
    }

    [Test]
    public async Task DeleteProduct_MarksSoftDeleteAndDeactivates()
    {
        var product = await CreateStoredProductAsync();

        Assert.That(await _service.DeleteProductAsync(product.Id), Is.True);

        var stored = _repository.Products.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.SoftDelete, Is.Not.Null);
            Assert.That(stored.IsActive, Is.False);
        });
    }
}
