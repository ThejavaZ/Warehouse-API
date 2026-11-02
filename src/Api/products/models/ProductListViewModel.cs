namespace Api.Products;

public class ProductListViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? SKU { get; set; }

    public string? Name { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }
}
