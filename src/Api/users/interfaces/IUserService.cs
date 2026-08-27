namespace Api.Users;

public interface IUserService
{
    Task<UserViewModel> GetUserByIdAsync(int id);
    Task<IEnumerable<UserListViewModel>> GetAllUsersAsync();
    Task<UserViewModel> CreateUserAsync(UserCreateDTO dto);
    Task<UserViewModel> UpdateUserAsync(int id, UserUpdateDTO dto);
    Task<bool> DeleteUserAsync(int id);
}