namespace Api.Inventory;

public interface IInventoryRepository
{
    Task<(IEnumerable<InventoryItemViewModel> Items, int TotalItems)> GetAllAsync(InventoryQuery query, CancellationToken cancellationToken);

    /// <summary>Ficha de existencias para un par almacén–product (sin tracking: es de solo lectura).</summary>
    Task<InventoryItem?> GetItemAsync(int warehouseId, int productId, CancellationToken cancellationToken);

    /// <summary>Ficha proyectada a ViewModel con los nombres de almacén y producto.</summary>
    Task<InventoryItemViewModel?> GetItemViewModelAsync(int warehouseId, int productId, CancellationToken cancellationToken);

    /// <summary>
    /// Suma stock con un UPDATE condicional y atómico. Devuelve el número de filas afectadas:
    /// 0 significa que la ficha aún no existe.
    /// </summary>
    Task<int> TryIncreaseQuantityAsync(int warehouseId, int productId, int quantity, CancellationToken cancellationToken);

    /// <summary>
    /// Resta stock SOLO si queda saldo suficiente (UPDATE ... WHERE Quantity &gt;= quantity).
    /// Devuelve el número de filas afectadas: 0 significa sin ficha o saldo insuficiente.
    /// </summary>
    Task<int> TryDecreaseQuantityAsync(int warehouseId, int productId, int quantity, CancellationToken cancellationToken);

    /// <summary>
    /// Crea la ficha si no existe. Si otra petición simultánea la creó primero (índice único
    /// WarehouseId+ProductId), reintenta el aumento y devuelve <c>false</c> solo si persiste el conflicto.
    /// </summary>
    Task<bool> AddAndIncreaseAsync(int warehouseId, int productId, int quantity, CancellationToken cancellationToken);
}
