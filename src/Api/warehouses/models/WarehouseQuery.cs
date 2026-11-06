using WMS.Application.Common;

namespace Api.Warehouses;

public class WarehouseQuery : PagedQuery
{
    public int? Id { get; set; }

    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
