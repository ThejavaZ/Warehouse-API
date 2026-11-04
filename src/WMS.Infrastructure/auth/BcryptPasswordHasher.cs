using WMS.Application.Ports;

namespace WMS.Infrastructure.Auth;

// Adaptador de salida del puerto IPasswordHasher.
public sealed class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) => BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
