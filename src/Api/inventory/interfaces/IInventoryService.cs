using Api.Common;

namespace Api.Inventory;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemViewModel>> GetAllAsync(InventoryQuery query, CancellationToken cancellationToken = default);

    /// <summary>Entrada de inventario: crea la ficha si no existe y suma la cantidad.</summary>
    Task<InventoryItemViewModel> RestockAsync(InventoryMovementDTO dto, CancellationToken cancellationToken = default);

    /// <summary>Salida de inventario: resta la cantidad; falla con 409 si no hay saldo suficiente.</summary>
    Task<InventoryItemViewModel> ConsumeAsync(InventoryMovementDTO dto, CancellationToken cancellationToken = default);
}
