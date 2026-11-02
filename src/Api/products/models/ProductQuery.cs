using Api.Common;

namespace Api.Products;

public class ProductQuery : PagedQuery
{
    public int? Id { get; set; }

    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
