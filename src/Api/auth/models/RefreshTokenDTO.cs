using System.ComponentModel.DataAnnotations;

namespace Api.Auth;

public class RefreshTokenDTO
{
    [Required(ErrorMessage = "El refresh token es obligatorio.")]
    public string RefreshToken { get; set; } = null!;
}
