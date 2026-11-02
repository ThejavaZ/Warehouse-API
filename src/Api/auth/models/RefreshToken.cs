using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Users;

namespace Api.Auth;

/// <summary>
/// Refresh token opaco almacenado como hash SHA-256: un volcado de la base de datos
/// no permite usarlo. Se rota en cada renovación y se revoca al cambiar la contraseña
/// o al dar de baja el usuario.
/// </summary>
[Table("refresh_tokens")]
public class RefreshToken
{
    [Key]
    public int Id { get; set; }

    /// <summary>Hash SHA-256 (hex) del token opaco entregado al cliente.</summary>
    [MaxLength(64)]
    public string Token { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    /// <summary>Hash del token que lo reemplazó tras una rotación; permite detectar reutilización.</summary>
    [MaxLength(64)]
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => RevokedAt == null && !IsExpired;
}
