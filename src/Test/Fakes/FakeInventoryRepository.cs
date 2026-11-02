using Api.Inventory;

namespace Api.Test.Fakes;

/// <summary>
/// Implementación en memoria de IInventoryRepository para pruebas unitarias del servicio.
/// Reproduce la semántica de los UPDATE condicionales reales: las operaciones solo
/// tienen efecto si la ficha existe y hay saldo suficiente.
/// </summary>
public class FakeInventoryRepository : IInventoryRepository
{
    public List<InventoryItem> Items { get; } = new();

    public Task<(IEnumerable<InventoryItemViewModel> Items, int TotalItems)> GetAllAsync(
        InventoryQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = Items.AsEnumerable().AsQueryable();

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.WarehouseId == query.WarehouseId.Value);
        }

        if (query.ProductId.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.ProductId == query.ProductId.Value);
        }

        var totalItems = dbQuery.Count();

        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(i => i.Id)
            : dbQuery.OrderBy(i => i.Id);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InventoryItemViewModel
            {
                Id = i.Id,
                WarehouseId = i.WarehouseId,
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToList();

        return Task.FromResult<(IEnumerable<InventoryItemViewModel>, int)>((items, totalItems));
    }

    public Task<InventoryItem?> GetItemAsync(int warehouseId, int productId, CancellationToken cancellationToken)
        => Task.FromResult(Items.SingleOrDefault(
            i => i.WarehouseId == warehouseId && i.ProductId == productId));

    public Task<InventoryItemViewModel?> GetItemViewModelAsync(
        int warehouseId,
        int productId,
        CancellationToken cancellationToken)
    {
        var item = Items.SingleOrDefault(i => i.WarehouseId == warehouseId && i.ProductId == productId);

        return Task.FromResult(item == null
            ? null
            : new InventoryItemViewModel
            {
                Id = item.Id,
                WarehouseId = item.WarehouseId,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            });
    }

    public Task<int> TryIncreaseQuantityAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var item = Items.SingleOrDefault(i => i.WarehouseId == warehouseId && i.ProductId == productId);

        if (item == null)
        {
            return Task.FromResult(0);
        }

        item.Quantity += quantity;
        item.UpdatedAt = DateTime.UtcNow;

        return Task.FromResult(1);
    }

    public Task<int> TryDecreaseQuantityAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var item = Items.SingleOrDefault(i => i.WarehouseId == warehouseId && i.ProductId == productId);

        // Misma garantía que el UPDATE ... WHERE Quantity >= quantity: sin saldo, no hay cambio.
        if (item == null || item.Quantity < quantity)
        {
            return Task.FromResult(0);
        }

        item.Quantity -= quantity;
        item.UpdatedAt = DateTime.UtcNow;

        return Task.FromResult(1);
    }

    public Task<bool> AddAndIncreaseAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        var item = Items.SingleOrDefault(i => i.WarehouseId == warehouseId && i.ProductId == productId);

        if (item != null)
        {
            // Simula la carrera perdida: el índice único impide crear la segunda ficha
            // y el reintento termina sumando sobre la existente.
            item.Quantity += quantity;
            return Task.FromResult(true);
        }

        Items.Add(new InventoryItem
        {
            Id = Items.Count == 0 ? 1 : Items.Max(i => i.Id) + 1,
            WarehouseId = warehouseId,
            ProductId = productId,
            Quantity = quantity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        return Task.FromResult(true);
    }
}
