namespace WMS.Application.Ports;

// Puerto de salida: el caso de uso decide QUién valida, no cómo se guarda la contraseña.
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
