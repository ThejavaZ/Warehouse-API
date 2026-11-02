namespace Api.Products;

public class ProductViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? SKU { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
