using System.ComponentModel.DataAnnotations;

using WMS.Domain.Entities;

namespace Api.Auth;

public class RefreshTokenDTO
{
    [Required(ErrorMessage = "El refresh token es obligatorio.")]
    public string RefreshToken { get; set; } = null!;
}
