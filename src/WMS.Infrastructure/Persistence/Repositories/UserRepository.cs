using Api.Common;
using Api.Users;
using Microsoft.EntityFrameworkCore;
using WMS.Infrastructure.Persistence;

namespace WMS.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public UserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<(IEnumerable<User> Users, int TotalItems)> GetAllUsersAsync(
        UserQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Consulta base: AsNoTracking porque es de solo lectura.
        IQueryable<User> dbQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.SoftDelete == null);

        // 2. Filtro de búsqueda por Name o Email (el IQueryable se compone de forma progresiva
        //    y EF Core lo traduce a un solo SQL con parámetros).
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var searchTerm = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(u =>
                (u.Name != null && u.Name.ToLower().Contains(searchTerm)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchTerm)));
        }

        // 3. Filtro por estado (solo cuando el cliente envía el valor).
        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(u => u.IsActive == query.IsActive.Value);
        }

        // 4. El conteo se ejecuta con los filtros ya aplicados y ANTES del Skip/Take.
        int totalItems = await dbQuery.CountAsync(cancellationToken);

        // 5. Ordenamiento.
        dbQuery = query.OrderDesc
            ? dbQuery.OrderByDescending(u => u.Id)
            : dbQuery.OrderBy(u => u.Id);

        // 6. Paginación. Los límites ya vienen validados por PagedQuery; nos aseguramos además
        //    de no generar un Skip negativo si el servicio no sanitizó la consulta.
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);
        int skip = (page - 1) * pageSize;

        var users = await dbQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (users, totalItems);
    }

    public async Task AddUserAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);

        // Un único SaveChangesAsync es atómico: EF Core envía todas las modificaciones
        // en una sola transacción implícita. BeginTransactionAsync solo haría falta
        // si participaran varios DbContext o se mezclaran operaciones ajenas a EF Core.
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);

        if (user == null)
            return;

        user.IsActive = false;
        user.SoftDelete = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
