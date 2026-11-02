using Api.Common;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Warehouses;

public class WarehouseRepository : IWarehouseRepository
{
    private readonly AppDbContext _context;

    public WarehouseRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Warehouse?> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);
    }

    public async Task<Warehouse?> GetWarehouseByNameAsync(string name, CancellationToken cancellationToken)
    {
        return await _context.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Name == name, cancellationToken);
    }

    public async Task<(IEnumerable<Warehouse> Warehouses, int TotalItems)> GetAllWarehousesAsync(
        WarehouseQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Consulta base: AsNoTracking porque es de solo lectura.
        IQueryable<Warehouse> dbQuery = _context.Warehouses
            .AsNoTracking()
            .Where(w => w.SoftDelete == null);

        // 2. Búsqueda por Name o Location (case-insensitive).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(w =>
                (w.Name != null && w.Name.ToLower().Contains(searchTerm)) ||
                (w.Location != null && w.Location.ToLower().Contains(searchTerm)));
        }

        // 3. Filtro por estado (solo cuando el cliente envía el valor).
        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(w => w.IsActive == query.IsActive.Value);
        }

        // 4. El conteo se ejecuta con los filtros ya aplicados y ANTES del Skip/Take.
        int totalItems = await dbQuery.CountAsync(cancellationToken);

        // 5. Ordenamiento estable por Id antes de paginar.
        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(w => w.Id)
            : dbQuery.OrderBy(w => w.Id);

        // 6. Paginación.
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);

        var warehouses = await dbQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (warehouses, totalItems);
    }

    public async Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        await _context.Warehouses.AddAsync(warehouse, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        _context.Warehouses.Update(warehouse);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteWarehouseAsync(int id, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses.FindAsync([id], cancellationToken);

        if (warehouse == null)
            return;

        warehouse.IsActive = false;
        warehouse.SoftDelete = DateTime.UtcNow;
        warehouse.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
