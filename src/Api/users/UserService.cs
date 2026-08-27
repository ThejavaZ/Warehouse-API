namespace Api.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public UserService(IUserRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserViewModel> GetUserByIdAsync(int id)
    {
        var user = await _repository.GetUserByIdAsync(id);

        if (user == null || user.SoftDelete != null)
            throw new KeyNotFoundException(
                $"User with ID {id} not found.");

        return MapToViewModel(user);
    }

    public async Task<IEnumerable<UserListViewModel>> GetAllUsersAsync()
    {
        var users = await _repository.GetAllUsersAsync();

        return users.Select(user => new UserListViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            IsActive = user.IsActive
        });
    }

    public async Task<UserViewModel> CreateUserAsync(UserCreateDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Password is required.");

        var existingUser = await _repository.GetUserByEmailAsync(dto.Email);

        if (existingUser != null && existingUser.SoftDelete == null)
            throw new ArgumentException(
                "A user with this email already exists.");

        var user = new User
        {
            UUID = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            Password = dto.Password,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.AddUserAsync(user);

        return MapToViewModel(user);
    }

    public async Task<UserViewModel> UpdateUserAsync(
        int id,
        UserUpdateDTO dto)
    {
        var user = await _repository.GetUserByIdAsync(id);

        if (user == null || user.SoftDelete != null)
            throw new KeyNotFoundException(
                $"User with ID {id} not found.");

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Name is required.");

        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email is required.");

        var emailUser = await _repository.GetUserByEmailAsync(dto.Email);

        if (emailUser != null &&
            emailUser.Id != id &&
            emailUser.SoftDelete == null)
        {
            throw new ArgumentException(
                "A user with this email already exists.");
        }

        user.Name = dto.Name;
        user.Email = dto.Email;
        user.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.Password))
            user.Password = dto.Password;

        await _repository.UpdateUserAsync(user);

        return MapToViewModel(user);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _repository.GetUserByIdAsync(id);

        if (user == null || user.SoftDelete != null)
            return false;

        await _repository.DeleteUserAsync(id);

        return true;
    }

    private static UserViewModel MapToViewModel(User user)
    {
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