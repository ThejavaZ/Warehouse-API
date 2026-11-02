using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Inventory;

public class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _context;

    public InventoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IEnumerable<InventoryItemViewModel> Items, int TotalItems)> GetAllAsync(
        InventoryQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Consulta base con filtros opcionales por almacén y/o producto.
        IQueryable<InventoryItem> dbQuery = _context.InventoryItems.AsNoTracking();

        if (query.WarehouseId.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.WarehouseId == query.WarehouseId.Value);
        }

        if (query.ProductId.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.ProductId == query.ProductId.Value);
        }

        // 2. El conteo se ejecuta con los filtros ya aplicados y ANTES del Skip/Take.
        int totalItems = await dbQuery.CountAsync(cancellationToken);

        // 3. Orden estable antes de paginar.
        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(i => i.Id)
            : dbQuery.OrderBy(i => i.Id);

        // 4. Paginación + proyección con los nombres de almacén y producto en la misma consulta.
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);

        var items = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new InventoryItemViewModel
            {
                Id = i.Id,
                WarehouseId = i.WarehouseId,
                WarehouseName = i.Warehouse.Name,
                ProductId = i.ProductId,
                ProductSku = i.Product.SKU,
                ProductName = i.Product.Name,
                Quantity = i.Quantity,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalItems);
    }

    public async Task<InventoryItem?> GetItemAsync(int warehouseId, int productId, CancellationToken cancellationToken)
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.WarehouseId == warehouseId && i.ProductId == productId,
                cancellationToken);
    }

    public async Task<InventoryItemViewModel?> GetItemViewModelAsync(int warehouseId, int productId, CancellationToken cancellationToken)
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && i.ProductId == productId)
            .Select(i => new InventoryItemViewModel
            {
                Id = i.Id,
                WarehouseId = i.WarehouseId,
                WarehouseName = i.Warehouse.Name,
                ProductId = i.ProductId,
                ProductSku = i.Product.SKU,
                ProductName = i.Product.Name,
                Quantity = i.Quantity,
                CreatedAt = i.CreatedAt,
                UpdatedAt = i.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> TryIncreaseQuantityAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        // UPDATE atómico en la propia BD: dos entradas simultáneas se aplican ambas,
        // en cualquier orden, sin perder unidades.
        return await _context.InventoryItems
            .Where(i => i.WarehouseId == warehouseId && i.ProductId == productId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Quantity, i => i.Quantity + quantity)
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<int> TryDecreaseQuantityAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        // La condición Quantity >= quantity vive EN el UPDATE: dos salidas simultáneas no
        // pueden dejar saldo negativo, porque cada UPDATE vuelve a evaluar el saldo real.
        return await _context.InventoryItems
            .Where(i => i.WarehouseId == warehouseId
                && i.ProductId == productId
                && i.Quantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(i => i.Quantity, i => i.Quantity - quantity)
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task<bool> AddAndIncreaseAsync(
        int warehouseId,
        int productId,
        int quantity,
        CancellationToken cancellationToken)
    {
        try
        {
            await _context.InventoryItems.AddAsync(
                new InventoryItem
                {
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    Quantity = quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Otra petición creó la ficha entre el UPDATE fallido y este INSERT
            // (índice único WarehouseId+ProductId): se limpia el intento local
            // y se reintenta el aumento sobre la ficha ganadora.
            foreach (var entry in _context.ChangeTracker.Entries<InventoryItem>()
                         .Where(e => e.State == EntityState.Added)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            int retried = await TryIncreaseQuantityAsync(warehouseId, productId, quantity, cancellationToken);

            return retried > 0;
        }
    }
}
