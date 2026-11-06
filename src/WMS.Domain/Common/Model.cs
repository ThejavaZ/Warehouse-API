using System.ComponentModel.DataAnnotations;

namespace WMS.Domain.Common;

public class Model
{
    [Key]
    public int Id { get; set; }
    public Guid UUID { get; set; } = Guid.NewGuid();
    [MaxLength(100)]
    public string? Name { get; set; }
    [MaxLength(500)]
    public string? Description { get; set; } = null;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SoftDelete { get; set; } = null;

}
