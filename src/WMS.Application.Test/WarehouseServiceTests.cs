using WMS.Application.Test.Fakes;
using WMS.Application.Warehouses;
using WMS.Domain.Entities;

namespace WMS.Application.Test;

[TestFixture]
public class WarehouseServiceTests
{
    private FakeWarehouseRepository _repository = null!;
    private WarehouseService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeWarehouseRepository();
        _service = new WarehouseService(_repository);
    }

    private static WarehouseCreateDTO NewDto(
        string name = "Almacén Central",
        string? location = "Madrid")
        => new() { Name = name, Location = location };

    private async Task<Warehouse> CreateStoredWarehouseAsync(
        string name = "Almacén Central",
        bool isActive = true)
    {
        await _service.CreateWarehouseAsync(NewDto(name));

        var warehouse = _repository.Warehouses.Single(w => w.Name == name);
        warehouse.IsActive = isActive;

        return warehouse;
    }

    [Test]
    public async Task CreateWarehouse_ReturnsViewModel()
    {
        var viewModel = await _service.CreateWarehouseAsync(NewDto());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Id, Is.GreaterThan(0));
            Assert.That(viewModel.UUID, Is.Not.EqualTo(Guid.Empty));
            Assert.That(viewModel.Name, Is.EqualTo("Almacén Central"));
            Assert.That(viewModel.Location, Is.EqualTo("Madrid"));
            Assert.That(viewModel.IsActive, Is.True);
        });
    }

    [Test]
    public void CreateWarehouse_DuplicateName_ThrowsArgumentException()
    {
        Assert.DoesNotThrowAsync(() => _service.CreateWarehouseAsync(NewDto()));

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.CreateWarehouseAsync(NewDto()));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task CreateWarehouse_WithSoftDeletedName_AllowsReuse()
    {
        var existing = await CreateStoredWarehouseAsync("Reutilizable");
        existing.SoftDelete = DateTime.UtcNow;

        Assert.DoesNotThrowAsync(() => _service.CreateWarehouseAsync(NewDto("Reutilizable")));
    }

    [Test]
    public void GetWarehouseById_WhenNotFound_ThrowsKeyNotFound()
    {
        var ex = Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetWarehouseByIdAsync(999));

        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task GetAllWarehouses_SearchAndIsActive_AreCombined()
    {
        await CreateStoredWarehouseAsync("Norte");
        await CreateStoredWarehouseAsync("Norte Archivo");
        var archived = _repository.Warehouses.Single(w => w.Name == "Norte Archivo");
        archived.IsActive = false;

        var result = await _service.GetAllWarehousesAsync(
            new WarehouseQuery { Search = "norte", IsActive = true });

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Name, Is.EqualTo("Norte"));
        });
    }

    [Test]
    public async Task UpdateWarehouse_WithAnotherWarehousesName_ThrowsArgumentException()
    {
        await CreateStoredWarehouseAsync("Owner");
        var target = await CreateStoredWarehouseAsync("Target");

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateWarehouseAsync(
            target.Id,
            new WarehouseUpdateDTO { Name = "Owner" }));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task DeleteWarehouse_MarksSoftDeleteAndDeactivates()
    {
        var warehouse = await CreateStoredWarehouseAsync();

        Assert.That(await _service.DeleteWarehouseAsync(warehouse.Id), Is.True);

        var stored = _repository.Warehouses.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.SoftDelete, Is.Not.Null);
            Assert.That(stored.IsActive, Is.False);
        });
    }

    [Test]
    public void DeleteWarehouse_WhenNotFound_ReturnsFalse()
    {
        Assert.That(_service.DeleteWarehouseAsync(999).Result, Is.False);
    }
}
