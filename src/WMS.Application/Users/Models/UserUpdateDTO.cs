using System.ComponentModel.DataAnnotations;

namespace WMS.Application.Users;

public class UserUpdateDTO
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Name { get; set; } = null!;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string Email { get; set; } = null!;

    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres si se desea actualizar.")]
    public string? Password { get; set; }
}
