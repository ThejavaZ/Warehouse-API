using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Warehouses;

[Table("warehouses")]
public class Warehouse
{
    [Key]
    public int Id { get; set; }

    public Guid UUID { get; set; } = Guid.NewGuid();

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    [MaxLength(200)]
    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SoftDelete { get; set; }
}
