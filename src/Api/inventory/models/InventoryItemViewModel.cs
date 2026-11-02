namespace Api.Inventory;

public class InventoryItemViewModel
{
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public int ProductId { get; set; }

    public string? ProductSku { get; set; }

    public string? ProductName { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
