using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Users;

[Table("users")]
public class User
{
    [Key]
    public int Id { get; set; }

    public Guid UUID { get; set; } = Guid.NewGuid();

    [MaxLength(50)]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    public string Email { get; set; } = null!;

    [MaxLength(100)]
    public string Password { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SoftDelete { get; set; }
}
