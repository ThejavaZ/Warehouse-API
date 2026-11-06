using WMS.Application.Warehouses;
using WMS.Domain.Entities;

namespace Api.Test.Fakes;

/// <summary>
/// Implementación en memoria de IWarehouseRepository para pruebas unitarias del servicio.
/// Reproduce el comportamiento de filtrado del repositorio real sin depender de EF Core.
/// </summary>
public class FakeWarehouseRepository : IWarehouseRepository
{
    public List<Warehouse> Warehouses { get; } = new();

    public Task<Warehouse?> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(Warehouses.SingleOrDefault(w => w.Id == id));

    public Task<Warehouse?> GetWarehouseByNameAsync(string name, CancellationToken cancellationToken)
        => Task.FromResult(Warehouses.SingleOrDefault(w => w.Name == name));

    public Task<(IEnumerable<Warehouse> Warehouses, int TotalItems)> GetAllWarehousesAsync(
        WarehouseQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = Warehouses.AsEnumerable().Where(w => w.SoftDelete == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(w =>
                (w.Name != null && w.Name.ToLower().Contains(term)) ||
                (w.Location != null && w.Location.ToLower().Contains(term)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(w => w.IsActive == query.IsActive.Value);
        }

        var totalItems = dbQuery.Count();

        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(w => w.Id)
            : dbQuery.OrderBy(w => w.Id);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var warehouses = dbQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IEnumerable<Warehouse>, int)>((warehouses, totalItems));
    }

    public Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        warehouse.Id = Warehouses.Count == 0 ? 1 : Warehouses.Max(w => w.Id) + 1;
        Warehouses.Add(warehouse);

        return Task.CompletedTask;
    }

    public Task UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken)
    {
        var index = Warehouses.FindIndex(w => w.Id == warehouse.Id);

        if (index >= 0)
        {
            Warehouses[index] = warehouse;
        }

        return Task.CompletedTask;
    }

    public Task DeleteWarehouseAsync(int id, CancellationToken cancellationToken)
    {
        var warehouse = Warehouses.SingleOrDefault(w => w.Id == id);

        if (warehouse != null)
        {
            warehouse.IsActive = false;
            warehouse.SoftDelete = DateTime.UtcNow;
            warehouse.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
}
