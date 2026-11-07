using Microsoft.AspNetCore.Identity;
using WMS.Domain.Entities;
using WMS.Infrastructure.Auth;

namespace WMS.Infrastructure.Test;

[TestFixture]
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();
    private readonly User _user = new();
    private const string Password = "Secret123!";

    [Test]
    public void Hash_ReturnsBcryptHashWithVersionPrefix()
    {
        var hash = _hasher.HashPassword(_user, Password);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Does.StartWith("$2"));
            Assert.That(hash, Has.Length.EqualTo(60));
        });
    }

    [Test]
    public void Hash_DoesNotExposePlaintext()
    {
        var hash = _hasher.HashPassword(_user, Password);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.Not.EqualTo(Password));
            Assert.That(hash, Does.Not.Contain(Password));
        });
    }

    [Test]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        // BCrypt aplica un salt distinto por hash: dos hashes del mismo
        // password no pueden ser idénticos.
        var first = _hasher.HashPassword(_user, Password);
        var second = _hasher.HashPassword(_user, Password);

        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(_hasher.VerifyHashedPassword(_user, first, Password),
            Is.EqualTo(PasswordVerificationResult.Success));
        Assert.That(_hasher.VerifyHashedPassword(_user, second, Password),
            Is.EqualTo(PasswordVerificationResult.Success));
    }

    [Test]
    public void Verify_WithMatchingPassword_ReturnsSuccess()
    {
        var hash = _hasher.HashPassword(_user, Password);

        var result = _hasher.VerifyHashedPassword(_user, hash, Password);

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Success));
    }

    [Test]
    public void Verify_WithWrongPassword_ReturnsFailed()
    {
        var hash = _hasher.HashPassword(_user, Password);

        var result = _hasher.VerifyHashedPassword(_user, hash, "WrongPassword!");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Failed));
    }

    [Test]
    public void VerifyHashedPassword_WithLowerWorkFactor_RequestsRehash()
    {
        // Hash legado con work factor 4: Identity debe pedir rehash para
        // actualizarlo al factor actual (11).
        var legacyHash = BCrypt.Net.BCrypt.HashPassword(Password, 4);

        var result = _hasher.VerifyHashedPassword(_user, legacyHash, Password);

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.SuccessRehashNeeded));
    }
}
