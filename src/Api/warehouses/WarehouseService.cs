using Api.Common;

namespace Api.Warehouses;

public class WarehouseService : IWarehouseService
{
    private readonly IWarehouseRepository _repository;

    public WarehouseService(IWarehouseRepository repository)
    {
        _repository = repository;
    }

    public async Task<WarehouseViewModel> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _repository.GetWarehouseByIdAsync(id, cancellationToken);

        if (warehouse == null || warehouse.SoftDelete != null)
            throw new KeyNotFoundException($"Warehouse with ID {id} not found.");

        return MapToViewModel(warehouse);
    }

    public async Task<PagedResult<WarehouseListViewModel>> GetAllWarehousesAsync(
        WarehouseQuery query,
        CancellationToken cancellationToken = default)
    {
        SanitizeQuery(query);

        var (warehouses, totalItems) = await _repository.GetAllWarehousesAsync(query, cancellationToken);

        var viewModels = warehouses.Select(warehouse => new WarehouseListViewModel
            {
                Id = warehouse.Id,
                UUID = warehouse.UUID,
                Name = warehouse.Name,
                Location = warehouse.Location,
                IsActive = warehouse.IsActive
            })
            .ToList();

        int totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return new PagedResult<WarehouseListViewModel>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Items = viewModels
        };
    }

    public async Task<WarehouseViewModel> CreateWarehouseAsync(
        WarehouseCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        // Mismo tratamiento que Email/SKU: comprobación en el Service + índice único filtrado en BD.
        var existingWarehouse = await _repository.GetWarehouseByNameAsync(dto.Name, cancellationToken);

        if (existingWarehouse != null && existingWarehouse.SoftDelete == null)
            throw new ArgumentException("A warehouse with this name already exists.", nameof(dto.Name));

        var warehouse = new Warehouse
        {
            UUID = Guid.NewGuid(),
            Name = dto.Name,
            Location = dto.Location,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddWarehouseAsync(warehouse, cancellationToken);

        return MapToViewModel(warehouse);
    }

    public async Task<WarehouseViewModel> UpdateWarehouseAsync(
        int id,
        WarehouseUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        var warehouse = await _repository.GetWarehouseByIdAsync(id, cancellationToken);

        if (warehouse == null || warehouse.SoftDelete != null)
            throw new KeyNotFoundException($"Warehouse with ID {id} not found.");

        var nameOwner = await _repository.GetWarehouseByNameAsync(dto.Name, cancellationToken);

        if (nameOwner != null && nameOwner.Id != id && nameOwner.SoftDelete == null)
            throw new ArgumentException("A warehouse with this name already exists.", nameof(dto.Name));

        warehouse.Name = dto.Name;
        warehouse.Location = dto.Location;
        warehouse.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateWarehouseAsync(warehouse, cancellationToken);

        return MapToViewModel(warehouse);
    }

    public async Task<bool> DeleteWarehouseAsync(int id, CancellationToken cancellationToken = default)
    {
        var warehouse = await _repository.GetWarehouseByIdAsync(id, cancellationToken);

        if (warehouse == null || warehouse.SoftDelete != null)
            return false;

        await _repository.DeleteWarehouseAsync(id, cancellationToken);

        return true;
    }

    private static void SanitizeQuery(WarehouseQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);
    }

    private static WarehouseViewModel MapToViewModel(Warehouse warehouse)
    {
        return new WarehouseViewModel
        {
            Id = warehouse.Id,
            UUID = warehouse.UUID,
            Name = warehouse.Name,
            Location = warehouse.Location,
            IsActive = warehouse.IsActive,
            CreatedAt = warehouse.CreatedAt,
            UpdatedAt = warehouse.UpdatedAt
        };
    }
}
