using WMS.Application.Users;
using WMS.Domain.Entities;

namespace Api.Test.Fakes;

/// <summary>
/// Implementación en memoria de IUserRepository para pruebas unitarias del servicio.
/// Reproduce el comportamiento de filtrado del repositorio real sin depender de EF Core.
/// </summary>
public class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = new();

    public Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken)
        => Task.FromResult(Users.SingleOrDefault(u => u.Id == id));

    public Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(Users.SingleOrDefault(u => u.Email == email));

    public Task<(IEnumerable<User> Users, int TotalItems)> GetAllUsersAsync(
        UserQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = Users.AsEnumerable().Where(u => u.SoftDelete == null);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(term)) ||
                (u.Email != null && u.Email.ToLower().Contains(term)));
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        var totalItems = dbQuery.Count();

        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(u => u.Id)
            : dbQuery.OrderBy(u => u.Id);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var users = dbQuery.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult<(IEnumerable<User>, int)>((users, totalItems));
    }

    public Task AddUserAsync(User user, CancellationToken cancellationToken)
    {
        user.Id = Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;
        Users.Add(user);

        return Task.CompletedTask;
    }

    public Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        var index = Users.FindIndex(u => u.Id == user.Id);

        if (index >= 0)
        {
            Users[index] = user;
        }

        return Task.CompletedTask;
    }

    public Task DeleteUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = Users.SingleOrDefault(u => u.Id == id);

        if (user != null)
        {
            user.IsActive = false;
            user.SoftDelete = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
}
