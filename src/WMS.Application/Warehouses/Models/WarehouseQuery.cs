using WMS.Application.Common;

namespace WMS.Application.Warehouses;

public class WarehouseQuery : PagedQuery
{
    public int? Id { get; set; }

    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
