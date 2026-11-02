using Api.Common;

namespace Api.Inventory;

public class InventoryQuery : PagedQuery
{
    public int? WarehouseId { get; set; }

    public int? ProductId { get; set; }
}
