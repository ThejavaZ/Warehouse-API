using WMS.Domain.Entities;

namespace WMS.Application.Warehouses;

public interface IWarehouseRepository
{
    Task<Warehouse?> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken);
    Task<Warehouse?> GetWarehouseByNameAsync(string name, CancellationToken cancellationToken);
    Task<(IEnumerable<Warehouse> Warehouses, int TotalItems)> GetAllWarehousesAsync(WarehouseQuery query, CancellationToken cancellationToken);
    Task AddWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken);
    Task UpdateWarehouseAsync(Warehouse warehouse, CancellationToken cancellationToken);
    Task DeleteWarehouseAsync(int id, CancellationToken cancellationToken);
}
