using Api.Data;
using Api.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Api.Test;

/// <summary>
/// Pruebas del repositorio contra una base de datos real (SQLite en memoria),
/// para verificar que las consultas IQueryable se traducen correctamente a SQL.
/// </summary>
[TestFixture]
public class UserRepositoryTests
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;
    private UserRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new UserRepository(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<User> SeedUserAsync(
        string email,
        string name,
        bool isActive = true,
        DateTime? softDelete = null)
    {
        var user = new User
        {
            UUID = Guid.NewGuid(),
            Name = name,
            Email = email,
            Password = BCrypt.Net.BCrypt.HashPassword("Secret123!"),
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            SoftDelete = softDelete
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(CancellationToken.None);

        return user;
    }

    [Test]
    public async Task GetAllUsersAsync_SearchMatchesNameOrEmail_IgnoringCase()
    {
        await SeedUserAsync("ana@test.com", "Ana Garcia");
        await SeedUserAsync("luis@test.com", "Luis Perez");
        await SeedUserAsync("contacto.ana@mail.com", "Servicio Contacto");

        var (users, total) = await _repository.GetAllUsersAsync(
            new UserQuery { Search = "ANA" },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(2));
            Assert.That(users.Select(u => u.Name), Is.EquivalentTo(new[] { "Ana Garcia", "Servicio Contacto" }));
        });
    }

    [Test]
    public async Task GetAllUsersAsync_SearchAndIsActive_AreCombined()
    {
        await SeedUserAsync("ana@test.com", "Ana Garcia", isActive: true);
        await SeedUserAsync("ana.baja@test.com", "Ana Baja", isActive: false);

        var (users, total) = await _repository.GetAllUsersAsync(
            new UserQuery { Search = "ana", IsActive = true },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(1));
            Assert.That(users.Single().Email, Is.EqualTo("ana@test.com"));
        });
    }

    [Test]
    public async Task GetAllUsersAsync_ExcludesSoftDeletedUsers()
    {
        await SeedUserAsync("activo@test.com", "Activo");
        await SeedUserAsync("borrado@test.com", "Borrado", softDelete: DateTime.UtcNow);

        var (users, total) = await _repository.GetAllUsersAsync(
            new UserQuery(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(1));
            Assert.That(users.Single().Email, Is.EqualTo("activo@test.com"));
        });
    }

    [Test]
    public async Task GetAllUsersAsync_CountsAfterFilters_AndPaginates()
    {
        for (int i = 1; i <= 5; i++)
        {
            await SeedUserAsync($"user{i}@test.com", $"User {i}");
        }

        var (users, total) = await _repository.GetAllUsersAsync(
            new UserQuery { Page = 2, PageSize = 2, OrderDesc = true },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(total, Is.EqualTo(5), "El conteo debe aplicarse con los filtros antes del Skip/Take.");
            Assert.That(users.Select(u => u.Name), Is.EqualTo(new[] { "User 3", "User 2" }));
        });
    }

    [Test]
    public async Task GetAllUsersAsync_PageBeyondRange_ReturnsEmptyWithTotal()
    {
        await SeedUserAsync("only@test.com", "Only User");

        var (users, total) = await _repository.GetAllUsersAsync(
            new UserQuery { Page = 99, PageSize = 10 },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(users, Is.Empty);
            Assert.That(total, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetAllUsersAsync_OrderAsc_ReturnsFirstUsers()
    {
        await SeedUserAsync("a@test.com", "First");
        await SeedUserAsync("b@test.com", "Second");

        var (users, _) = await _repository.GetAllUsersAsync(
            new UserQuery { OrderDesc = false },
            CancellationToken.None);

        Assert.That(users.Select(u => u.Name), Is.EqualTo(new[] { "First", "Second" }));
    }

    [Test]
    public async Task UniqueEmail_AllowsReuseAfterSoftDelete()
    {
        await SeedUserAsync("reusable@test.com", "Original", softDelete: DateTime.UtcNow);

        Assert.DoesNotThrowAsync(async () =>
        {
            await SeedUserAsync("reusable@test.com", "Nueva cuenta");
        });
    }

    [Test]
    public void UniqueEmail_RejectsActiveDuplicates()
    {
        SeedUserAsync("duplicado@test.com", "Primero").GetAwaiter().GetResult();

        var ex = Assert.Throws<DbUpdateException>(() =>
        {
            SeedUserAsync("duplicado@test.com", "Segundo").GetAwaiter().GetResult();
        });

        Assert.That(ex, Is.Not.Null, "La base de datos debe rechazar correos duplicados activos.");
    }

    [Test]
    public async Task GetUserByEmailAsync_ReturnsMatchingUser()
    {
        await SeedUserAsync("findme@test.com", "Find Me");

        var user = await _repository.GetUserByEmailAsync("findme@test.com", CancellationToken.None);

        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Name, Is.EqualTo("Find Me"));
    }
}
