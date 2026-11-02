using Api.Auth;
using Api.Common;
using Microsoft.AspNetCore.Identity;

namespace Api.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;

    public UserService(
        IUserRepository repository,
        IPasswordHasher<User> passwordHasher,
        IRefreshTokenRepository refreshTokenRepository)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _refreshTokenRepository = refreshTokenRepository;
    }

    public async Task<UserViewModel> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(id, cancellationToken);

        if (user == null || user.SoftDelete != null)
            throw new KeyNotFoundException($"User with ID {id} not found.");

        return MapToViewModel(user);
    }

    public async Task<PagedResult<UserListViewModel>> GetAllUsersAsync(
        UserQuery query,
        CancellationToken cancellationToken = default)
    {
        SanitizeQuery(query);

        var (users, totalItems) = await _repository.GetAllUsersAsync(query, cancellationToken);

        var viewModels = users.Select(user => new UserListViewModel
            {
                Id = user.Id,
                UUID = user.UUID,
                Name = user.Name,
                Email = user.Email,
                IsActive = user.IsActive
            })
            .ToList();

        int totalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize);

        return new PagedResult<UserListViewModel>
        {
            Page = query.Page,
            PageSize = query.PageSize,
            TotalPages = totalPages,
            TotalItems = totalItems,
            Items = viewModels
        };
    }

    public async Task<UserViewModel> CreateUserAsync(
        UserCreateDTO dto,
        CancellationToken cancellationToken = default)
    {
        var existingUser = await _repository.GetUserByEmailAsync(dto.Email, cancellationToken);

        if (existingUser != null && existingUser.SoftDelete == null)
            throw new ArgumentException("A user with this email already exists.", nameof(dto.Email));

        var user = new User
        {
            UUID = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Nunca en texto plano: se almacena el hash con salt generado por BCrypt.
        user.Password = _passwordHasher.HashPassword(user, dto.Password);

        await _repository.AddUserAsync(user, cancellationToken);

        return MapToViewModel(user);
    }

    public async Task<UserViewModel> UpdateUserAsync(
        int id,
        UserUpdateDTO dto,
        CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(id, cancellationToken);

        if (user == null || user.SoftDelete != null)
            throw new KeyNotFoundException($"User with ID {id} not found.");

        var emailOwner = await _repository.GetUserByEmailAsync(dto.Email, cancellationToken);

        if (emailOwner != null && emailOwner.Id != id && emailOwner.SoftDelete == null)
            throw new ArgumentException("A user with this email already exists.", nameof(dto.Email));

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            user.Password = _passwordHasher.HashPassword(user, dto.Password);

            // Cambio de contraseña → mueren todas las sesiones abiertas con refresh tokens anteriores.
            await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        await _repository.UpdateUserAsync(user, cancellationToken);

        return MapToViewModel(user);
    }

    public async Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await _repository.GetUserByIdAsync(id, cancellationToken);

        if (user == null || user.SoftDelete != null)
            return false;

        await _repository.DeleteUserAsync(id, cancellationToken);

        // Baja del usuario → revoca sus refresh tokens para cerrar sus sesiones.
        await _refreshTokenRepository.RevokeAllForUserAsync(id, cancellationToken);

        return true;
    }

    private static void SanitizeQuery(UserQuery query)
    {
        query.Page = Math.Max(1, query.Page);
        query.PageSize = Math.Clamp(query.PageSize, 1, PagedQuery.MaxPageSize);
    }

    private static UserViewModel MapToViewModel(User user)
    {
        // El ViewModel no expone Password ni ningún otro dato sensible.
        return new UserViewModel
        {
            Id = user.Id,
            UUID = user.UUID,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
