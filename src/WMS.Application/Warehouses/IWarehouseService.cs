using WMS.Application.Common;

namespace WMS.Application.Warehouses;

public interface IWarehouseService
{
    Task<WarehouseViewModel> GetWarehouseByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<WarehouseListViewModel>> GetAllWarehousesAsync(WarehouseQuery query, CancellationToken cancellationToken = default);
    Task<WarehouseViewModel> CreateWarehouseAsync(WarehouseCreateDTO dto, CancellationToken cancellationToken = default);
    Task<WarehouseViewModel> UpdateWarehouseAsync(int id, WarehouseUpdateDTO dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteWarehouseAsync(int id, CancellationToken cancellationToken = default);
}
