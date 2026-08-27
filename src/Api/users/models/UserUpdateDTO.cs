using System.ComponentModel.DataAnnotations;

namespace Api.Users;

public class UserUpdateDTO
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MinLength(8)]
    [MaxLength(100)]
    public string? Password { get; set; }
}