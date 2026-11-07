using System.Reflection;
using Api.Test.Fakes;
using WMS.Application.Users;
using WMS.Domain.Entities;
using WMS.Infrastructure.Auth;

namespace Api.Test;

[TestFixture]
public class UserServiceTests
{
    private FakeUserRepository _repository = null!;
    private FakeRefreshTokenRepository _refreshTokens = null!;
    private UserService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new FakeUserRepository();
        _refreshTokens = new FakeRefreshTokenRepository();
        _service = new UserService(_repository, new BCryptPasswordHasher(), _refreshTokens);
    }

    private static UserCreateDTO NewDto(
        string name = "Juan Perez",
        string email = "juan@test.com",
        string password = "Secret123!")
        => new() { Name = name, Email = email, Password = password };

    private async Task<User> CreateStoredUserAsync(
        string email = "stored@test.com",
        string name = "Stored User",
        bool isActive = true)
    {
        await _service.CreateUserAsync(NewDto(name, email));

        var user = _repository.Users.Single(u => u.Email == email);
        user.IsActive = isActive;

        return user;
    }

    [Test]
    public async Task CreateUser_StoresHashedPassword_NotPlainText()
    {
        await _service.CreateUserAsync(NewDto());

        var stored = _repository.Users.Single();

        Assert.Multiple(() =>
        {
            Assert.That(stored.Password, Is.Not.Null.And.Not.EqualTo("Secret123!"));
            Assert.That(stored.Password, Does.StartWith("$2"), "El hash debe tener el formato BCrypt.");
            Assert.That(BCrypt.Net.BCrypt.Verify("Secret123!", stored.Password), Is.True);
        });
    }

    [Test]
    public async Task CreateUser_UsesSalt_GeneratingDifferentHashes()
    {
        await _service.CreateUserAsync(NewDto(email: "a@test.com", password: "SamePass123!"));
        await _service.CreateUserAsync(NewDto(email: "b@test.com", password: "SamePass123!"));

        var first = _repository.Users.Single(u => u.Email == "a@test.com").Password;
        var second = _repository.Users.Single(u => u.Email == "b@test.com").Password;

        Assert.That(first, Is.Not.EqualTo(second), "La misma contraseña debe producir hashes distintos por el salt.");
    }

    [Test]
    public async Task CreateUser_DuplicateEmail_ThrowsArgumentException()
    {
        await _service.CreateUserAsync(NewDto());

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.CreateUserAsync(NewDto()));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task CreateUser_WithSoftDeletedEmail_AllowsReuse()
    {
        var existing = await CreateStoredUserAsync("reusable@test.com");
        existing.SoftDelete = DateTime.UtcNow;

        Assert.DoesNotThrowAsync(() => _service.CreateUserAsync(NewDto(email: "reusable@test.com")));
    }

    [Test]
    public async Task CreateUser_ReturnsViewModel_WithoutPassword()
    {
        var viewModel = await _service.CreateUserAsync(NewDto());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Id, Is.GreaterThan(0));
            Assert.That(viewModel.Name, Is.EqualTo("Juan Perez"));
            Assert.That(viewModel.Email, Is.EqualTo("juan@test.com"));
            Assert.That(viewModel.IsActive, Is.True);
            Assert.That(viewModel.UUID, Is.Not.EqualTo(Guid.Empty));
        });
    }

    [Test]
    public void GetUserById_WhenNotFound_ThrowsKeyNotFound()
    {
        var ex = Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetUserByIdAsync(999));

        Assert.That(ex!.Message, Does.Contain("999"));
    }

    [Test]
    public async Task GetUserById_WhenSoftDeleted_ThrowsKeyNotFound()
    {
        var user = await CreateStoredUserAsync();
        user.SoftDelete = DateTime.UtcNow;

        Assert.ThrowsAsync<KeyNotFoundException>(() => _service.GetUserByIdAsync(user.Id));
    }

    [Test]
    public async Task GetAllUsers_EmptyRepository_ReturnsZeroTotalPagesAndEmptyItems()
    {
        var result = await _service.GetAllUsersAsync(new UserQuery());

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.Zero);
            Assert.That(result.TotalPages, Is.Zero);
            Assert.That(result.Items, Is.Empty);
            Assert.That(result.HasNextPage, Is.False);
        });
    }

    [Test]
    [TestCase(0, 1)]
    [TestCase(-5, 1)]
    [TestCase(500, 100)]
    [TestCase(20, 20)]
    public async Task GetAllUsers_InvalidPageSize_IsSanitized(int requested, int expected)
    {
        await CreateStoredUserAsync();

        var result = await _service.GetAllUsersAsync(new UserQuery { PageSize = requested });

        Assert.That(result.PageSize, Is.EqualTo(expected));
    }

    [Test]
    public async Task GetAllUsers_NegativePage_IsSanitizedToOne()
    {
        await CreateStoredUserAsync();

        var result = await _service.GetAllUsersAsync(new UserQuery { Page = -3 });

        Assert.That(result.Page, Is.EqualTo(1));
    }

    [Test]
    public async Task GetAllUsers_PageBeyondRange_ReturnsEmptyItemsWithRealTotal()
    {
        for (int i = 1; i <= 3; i++)
        {
            await CreateStoredUserAsync($"user{i}@test.com", $"User {i}");
        }

        var result = await _service.GetAllUsersAsync(new UserQuery { Page = 10, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(3));
            Assert.That(result.TotalPages, Is.EqualTo(2));
            Assert.That(result.Items, Is.Empty);
        });
    }

    [Test]
    public async Task GetAllUsers_SearchAndIsActive_AreCombined()
    {
        await CreateStoredUserAsync("ana.garcia@test.com", "Ana Garcia", isActive: true);
        await CreateStoredUserAsync("ana.lopez@test.com", "Ana Lopez", isActive: false);
        await CreateStoredUserAsync("luis@test.com", "Luis Perez", isActive: true);

        var result = await _service.GetAllUsersAsync(
            new UserQuery { Search = "ana", IsActive = true });

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Name, Is.EqualTo("Ana Garcia"));
        });
    }

    [Test]
    public async Task GetAllUsers_SoftDeletedUsers_AreExcluded()
    {
        var deleted = await CreateStoredUserAsync("deleted@test.com", "Deleted User");
        deleted.SoftDelete = DateTime.UtcNow;
        await CreateStoredUserAsync("active@test.com", "Active User");

        var result = await _service.GetAllUsersAsync(new UserQuery());

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalItems, Is.EqualTo(1));
            Assert.That(result.Items.Single().Email, Is.EqualTo("active@test.com"));
        });
    }

    [Test]
    public async Task GetAllUsers_ViewModels_DoNotExposePassword()
    {
        await CreateStoredUserAsync();

        var result = await _service.GetAllUsersAsync(new UserQuery());

        Assert.That(result.Items.Single(), Is.Not.Property("Password"));
    }

    [Test]
    public void UserViewModelTypes_DoNotDeclarePasswordProperty()
    {
        var propertyNames = typeof(UserViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Concat(typeof(UserListViewModel)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name))
            .ToList();

        Assert.That(propertyNames, Has.None.EqualTo("Password"));
    }

    [Test]
    public void UpdateUser_WhenNotFound_ThrowsKeyNotFound()
    {
        Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.UpdateUserAsync(999, new UserUpdateDTO { Name = "X", Email = "x@test.com" }));
    }

    [Test]
    public async Task UpdateUser_WithAnotherUsersEmail_ThrowsArgumentException()
    {
        await CreateStoredUserAsync("owner@test.com");
        var target = await CreateStoredUserAsync("target@test.com");

        var ex = Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO { Name = "Target", Email = "owner@test.com" }));

        Assert.That(ex!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task UpdateUser_WithOwnEmail_DoesNotThrow()
    {
        var target = await CreateStoredUserAsync("same@test.com", "Original Name");

        var updated = await _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO { Name = "Renamed", Email = "same@test.com" });

        Assert.That(updated.Name, Is.EqualTo("Renamed"));
    }

    [Test]
    public async Task UpdateUser_WithoutPassword_KeepsCurrentHash()
    {
        var target = await CreateStoredUserAsync();
        var originalHash = target.Password;

        await _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO { Name = "New Name", Email = target.Email! });

        Assert.That(_repository.Users.Single().Password, Is.EqualTo(originalHash));
    }

    [Test]
    public async Task UpdateUser_WithPassword_RehashesIt()
    {
        var target = await CreateStoredUserAsync();
        var originalHash = target.Password;

        await _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO
            {
                Name = target.Name!,
                Email = target.Email!,
                Password = "NewSecret456!"
            });

        var stored = _repository.Users.Single();

        Assert.Multiple(() =>
        {
            Assert.That(stored.Password, Is.Not.EqualTo(originalHash));
            Assert.That(BCrypt.Net.BCrypt.Verify("NewSecret456!", stored.Password), Is.True);
        });
    }

    [Test]
    public async Task DeleteUser_WhenNotFound_ReturnsFalse()
    {
        Assert.That(await _service.DeleteUserAsync(999), Is.False);
    }

    [Test]
    public async Task DeleteUser_WhenAlreadySoftDeleted_ReturnsFalse()
    {
        var user = await CreateStoredUserAsync();
        user.SoftDelete = DateTime.UtcNow;

        Assert.That(await _service.DeleteUserAsync(user.Id), Is.False);
    }

    [Test]
    public async Task DeleteUser_MarksSoftDeleteAndDeactivates()
    {
        var user = await CreateStoredUserAsync();

        Assert.That(await _service.DeleteUserAsync(user.Id), Is.True);

        var stored = _repository.Users.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.SoftDelete, Is.Not.Null);
            Assert.That(stored.IsActive, Is.False);
        });
    }

    [Test]
    public async Task UpdateUser_WithPassword_RevokesExistingRefreshTokens()
    {
        var target = await CreateStoredUserAsync();
        SeedRefreshToken(target.Id);

        await _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO { Name = target.Name!, Email = target.Email!, Password = "NewSecret456!" });

        Assert.That(_refreshTokens.Tokens.Single().RevokedAt, Is.Not.Null,
            "Cambiar la contraseña debe cerrar las sesiones abiertas.");
    }

    [Test]
    public async Task UpdateUser_WithoutPassword_KeepsRefreshTokensActive()
    {
        var target = await CreateStoredUserAsync();
        SeedRefreshToken(target.Id);

        await _service.UpdateUserAsync(
            target.Id,
            new UserUpdateDTO { Name = "Renamed", Email = target.Email! });

        Assert.That(_refreshTokens.Tokens.Single().RevokedAt, Is.Null,
            "Sin cambio de contraseña no deben revocarse las sesiones.");
    }

    [Test]
    public async Task DeleteUser_RevokesExistingRefreshTokens()
    {
        var user = await CreateStoredUserAsync();
        SeedRefreshToken(user.Id);

        Assert.That(await _service.DeleteUserAsync(user.Id), Is.True);

        Assert.That(_refreshTokens.Tokens.Single().RevokedAt, Is.Not.Null,
            "La baja del usuario debe cerrar sus sesiones.");
    }

    private void SeedRefreshToken(int userId)
    {
        _refreshTokens.Tokens.Add(new WMS.Domain.Entities.RefreshToken
        {
            Id = 1,
            Token = "stored-hash",
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });
    }
}
