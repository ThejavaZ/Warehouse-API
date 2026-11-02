using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Products;

[Table("products")]
public class Product
{
    [Key]
    public int Id { get; set; }

    public Guid UUID { get; set; } = Guid.NewGuid();

    [MaxLength(50)]
    public string SKU { get; set; } = null!;

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>Precio con precisión decimal (18,2); nunca double, que pierde precisión con dinero.</summary>
    public decimal Price { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SoftDelete { get; set; }
}
