using WMS.Application.Common;

namespace WMS.Application.Products;

public class ProductQuery : PagedQuery
{
    public int? Id { get; set; }

    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
