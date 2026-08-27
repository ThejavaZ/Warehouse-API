using System.ComponentModel.DataAnnotations;

namespace Api.Users;

public class User
{
    [Key]
    public int Id { get; set; }

    public Guid UUID { get; set; }

    [MaxLength(50)]
    public string? Name { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SoftDelete { get; set; }
}