using System.ComponentModel.DataAnnotations;

namespace Api.Auth;

public class LoginDTO
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = null!;
}
