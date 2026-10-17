using Api.Users;
using Microsoft.AspNetCore.Identity;

namespace Api.Security;

/// <summary>
/// Hash de contraseñas con BCrypt (salt integrado, work factor 11)
/// expuesto a través de la abstracción IPasswordHasher de ASP.NET Core Identity.
/// </summary>
public class BCryptPasswordHasher : IPasswordHasher<User>
{
    private const int WorkFactor = 11;

    public string HashPassword(User user, string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        User user,
        string hashedPassword,
        string providedPassword)
    {
        if (!BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        // Si el coste almacenado difiere del work factor actual, se recomputa en el próximo guardado.
        return GetWorkFactor(hashedPassword) != WorkFactor
            ? PasswordVerificationResult.SuccessRehashNeeded
            : PasswordVerificationResult.Success;
    }

    private static int GetWorkFactor(string bcryptHash)
    {
        // Formato: $2a$11$salt+hash → el segundo segmento es el coste (work factor).
        var parts = bcryptHash.Split('$');

        return parts.Length > 2 && int.TryParse(parts[2], out var workFactor)
            ? workFactor
            : WorkFactor;
    }
}
