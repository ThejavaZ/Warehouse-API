using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WMS.Application.Ports;

namespace WMS.Infrastructure.Auth;

// Adaptador de salida del puerto ITokenService: firma JWT con la clave de configuración.
public sealed class JwtTokenService(IConfiguration configuration) : ITokenService
{
    private readonly SymmetricSecurityKey _signingKey = CreateSigningKey(configuration);

    public TokenResult Generate(string username)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Auth:Issuer"],
            audience: configuration["Auth:Audience"],
            claims: [new Claim(ClaimTypes.Name, username)],
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new TokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static SymmetricSecurityKey CreateSigningKey(IConfiguration configuration)
    {
        // Sin SigningKey configurado se genera una clave efímera: nunca se hardcodea en el repo.
        var configured = configuration["Auth:SigningKey"];
        var keyBytes = string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(configured);
        return new SymmetricSecurityKey(keyBytes);
    }
}
