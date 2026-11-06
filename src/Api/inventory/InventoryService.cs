using Api.Products;
using Api.Warehouses;
using WMS.Application.Common;
using WMS.Domain.Common;

namespace Api.Inventory;

public class InventoryService : IInventoryService
{
    private readonly IInventoryRepository _repository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;

    public InventoryService(
        IInventoryRepository repository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository)
    {
        _repository = repository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
    }

    public async Task<PagedResult<InventoryItemViewModel>> GetAllAsync(
        InventoryQuery query,
        CancellationToken cancellationToken = default)
    {
        SanitizeQuery(query);

        var (items, totalItems) = await _repository.GetAllAsync(query, cancellationToken);

        int totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return new PagedResult<InventoryItemViewModel>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Items = items.ToList()
        };
    }

    public async Task<InventoryItemViewModel> RestockAsync(
        InventoryMovementDTO dto,
        CancellationToken cancellationToken = default)
    {
        await ValidateMovementAsync(dto, cancellationToken);

        // Si la ficha ya existe, la suma es un UPDATE condicional atómico.
        // Si no, se crea con la cantidad inicial (o se reintenta si otra petición ganó la carrera).
        bool applied = await _repository.TryIncreaseQuantityAsync(
                dto.WarehouseId, dto.ProductId, dto.Quantity, cancellationToken) > 0
            || await _repository.AddAndIncreaseAsync(
                dto.WarehouseId, dto.ProductId, dto.Quantity, cancellationToken);

        if (!applied)
        {
            throw new DomainException(
                "stock_update_conflict",
                "The stock could not be updated due to a concurrent operation. Try again.",
                409);
        }

        return await GetItemViewModelAsync(dto.WarehouseId, dto.ProductId, cancellationToken);
    }

    public async Task<InventoryItemViewModel> ConsumeAsync(
        InventoryMovementDTO dto,
        CancellationToken cancellationToken = default)
    {
        await ValidateMovementAsync(dto, cancellationToken);

        // UPDATE ... WHERE Quantity >= qty: si devuelve 0 filas, o no hay ficha o no hay saldo.
        int updated = await _repository.TryDecreaseQuantityAsync(
            dto.WarehouseId, dto.ProductId, dto.Quantity, cancellationToken);

        if (updated == 0)
        {
            var item = await _repository.GetItemAsync(dto.WarehouseId, dto.ProductId, cancellationToken);

            if (item == null)
            {
                throw new DomainException(
                    "no_stock_record",
                    $"There is no stock record for product {dto.ProductId} in warehouse {dto.WarehouseId}.",
                    404);
            }

            throw new DomainException(
                "insufficient_stock",
                $"Insufficient stock: requested {dto.Quantity} but only {item.Quantity} available.",
                409);
        }

        return await GetItemViewModelAsync(dto.WarehouseId, dto.ProductId, cancellationToken);
    }

    private async Task ValidateMovementAsync(InventoryMovementDTO dto, CancellationToken cancellationToken)
    {
        // Defensa en profundidad: el DTO ya valida Range(1, Max), pero la regla
        // "nunca cantidades inválidas" se comprueba antes de tocar la base de datos.
        if (dto.Quantity < 1)
        {
            throw new DomainException("invalid_quantity", "Quantity must be at least 1.", 400, nameof(dto.Quantity));
        }

        var warehouse = await _warehouseRepository.GetWarehouseByIdAsync(dto.WarehouseId, cancellationToken);

        if (warehouse == null || warehouse.SoftDelete != null)
            throw new KeyNotFoundException($"Warehouse with ID {dto.WarehouseId} not found.");

        if (!warehouse.IsActive)
            throw new DomainException("inactive_warehouse", $"Warehouse {dto.WarehouseId} is inactive.", 400);

        var product = await _productRepository.GetProductByIdAsync(dto.ProductId, cancellationToken);

        if (product == null || product.SoftDelete != null)
            throw new KeyNotFoundException($"Product with ID {dto.ProductId} not found.");

        if (!product.IsActive)
            throw new DomainException("inactive_product", $"Product {dto.ProductId} is inactive.", 400);
    }

    private async Task<InventoryItemViewModel> GetItemViewModelAsync(
        int warehouseId,
        int productId,
        CancellationToken cancellationToken)
    {
        var item = await _repository.GetItemViewModelAsync(warehouseId, productId, cancellationToken);

        if (item == null)
        {
            // No debería ocurrir: el movimiento acaba de aplicarse.
            throw new DomainException(
                "stock_update_conflict",
                "The stock record disappeared after the update. Try again.",
                409);
        }

        return item;
    }

    private static void SanitizeQuery(InventoryQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);
    }
}
