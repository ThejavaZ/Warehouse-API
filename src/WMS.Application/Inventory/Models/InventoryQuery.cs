using WMS.Application.Common;

namespace WMS.Application.Inventory;

public class InventoryQuery : PagedQuery
{
    public int? WarehouseId { get; set; }

    public int? ProductId { get; set; }
}
