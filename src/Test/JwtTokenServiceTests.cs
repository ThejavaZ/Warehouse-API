using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Api.Auth;
using Microsoft.Extensions.Options;
using WMS.Domain.Entities;

namespace Api.Test;

[TestFixture]
public class JwtTokenServiceTests
{
    private JwtTokenService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new JwtTokenService(Options.Create(new AuthOptions
        {
            Secret = "unit-test-secret-with-at-least-32-characters",
            AccessTokenMinutes = 15
        }));
    }

    private static User NewUser() => new()
    {
        Id = 7,
        UUID = Guid.NewGuid(),
        Name = "Ana Garcia",
        Email = "ana@test.com",
        Password = "hashed"
    };

    [Test]
    public void GenerateAccessToken_CarriesUserClaims()
    {
        var (token, expiresIn) = _service.GenerateAccessToken(NewUser());

        var claims = new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims.ToList();

        Assert.Multiple(() =>
        {
            Assert.That(claims, Has.Some.Matches<Claim>(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "7"));
            Assert.That(claims, Has.Some.Matches<Claim>(c => c.Type == ClaimTypes.Email && c.Value == "ana@test.com"));
            Assert.That(claims, Has.Some.Matches<Claim>(c => c.Type == "jti"));
            Assert.That(expiresIn, Is.EqualTo(15 * 60));
        });
    }

    [Test]
    public void GenerateRefreshToken_ProducesUniqueValues()
    {
        var first = _service.GenerateRefreshToken();
        var second = _service.GenerateRefreshToken();

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first, Has.Length.GreaterThan(40), "Debe traer suficiente entropía.");
            Assert.That(first, Does.Not.Match(@"[+/=]"), "El token usa base64url sin caracteres reservados.");
        });
    }

    [Test]
    public void HashRefreshToken_IsStable_And64HexChars()
    {
        var token = _service.GenerateRefreshToken();
        var hash = _service.HashRefreshToken(token);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.EqualTo(_service.HashRefreshToken(token)), "El mismo token produce el mismo hash.");
            Assert.That(hash, Is.Not.EqualTo(token), "En BD se guarda el hash, nunca el valor.");
            Assert.That(hash, Does.Match("^[0-9a-f]{64}$"));
        });
    }
}
