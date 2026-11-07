using Microsoft.AspNetCore.Identity;
using WMS.Domain.Entities;
using WMS.Infrastructure.Auth;

namespace Api.Test;

[TestFixture]
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();
    private readonly User _user = new() { Name = "Test", Email = "test@test.com" };

    [Test]
    public void HashPassword_DoesNotStorePlaintext()
    {
        var hash = _hasher.HashPassword(_user, "Secret123!");

        Assert.Multiple(() =>
        {
            Assert.That(hash, Is.Not.EqualTo("Secret123!"));
            Assert.That(hash, Does.StartWith("$2"));
        });
    }

    [Test]
    public void VerifyHashedPassword_WithCorrectPassword_Succeeds()
    {
        var hash = _hasher.HashPassword(_user, "Secret123!");

        var result = _hasher.VerifyHashedPassword(_user, hash, "Secret123!");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Success));
    }

    [Test]
    public void VerifyHashedPassword_WithWrongPassword_Fails()
    {
        var hash = _hasher.HashPassword(_user, "Secret123!");

        var result = _hasher.VerifyHashedPassword(_user, hash, "WrongPassword!");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.Failed));
    }

    [Test]
    public void VerifyHashedPassword_WithLowerWorkFactor_RequestsRehash()
    {
        var legacyHash = BCrypt.Net.BCrypt.HashPassword("Secret123!", 4);

        var result = _hasher.VerifyHashedPassword(_user, legacyHash, "Secret123!");

        Assert.That(result, Is.EqualTo(PasswordVerificationResult.SuccessRehashNeeded));
    }
}
