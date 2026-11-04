namespace WMS.Application.Ports;

public record TokenResult(string Token, DateTimeOffset ExpiresAt);

// Puerto de salida: el caso de uso pide un token, no conoce JWT ni dónde vive la clave.
public interface ITokenService
{
    TokenResult Generate(string username);
}
