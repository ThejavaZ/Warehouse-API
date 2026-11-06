using WMS.Infrastructure.Auth;

namespace WMS.Infrastructure.Test;

[TestFixture]
public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher = new();
    private const string Password = "Secret123!";

    [Test]
    public void Hash_ReturnsBcryptHashWithVersionPrefix()
    {
        var hash = _hasher.Hash(Password);

        Assert.Multiple(() =>
        {
            Assert.That(hash, Does.StartWith("$2"));
            Assert.That(hash, Has.Length.EqualTo(60));
        });
    }

    [Test]
    public void Hash_DoesNotExposePlaintext()
    {
        var hash = _hasher.Hash(Password);

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
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        Assert.That(first, Is.Not.EqualTo(second));
        Assert.That(_hasher.Verify(Password, first), Is.True);
        Assert.That(_hasher.Verify(Password, second), Is.True);
    }

    [Test]
    public void Verify_WithMatchingPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Password);

        var result = _hasher.Verify(Password, hash);

        Assert.That(result, Is.True);
    }

    [Test]
    public void Verify_WithWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash(Password);

        var result = _hasher.Verify("WrongPassword!", hash);

        Assert.That(result, Is.False);
    }
}
