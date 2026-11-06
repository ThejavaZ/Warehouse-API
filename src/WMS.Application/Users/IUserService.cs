using WMS.Application.Common;

namespace WMS.Application.Users;

public interface IUserService
{
    Task<UserViewModel> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResult<UserListViewModel>> GetAllUsersAsync(UserQuery query, CancellationToken cancellationToken = default);
    Task<UserViewModel> CreateUserAsync(UserCreateDTO dto, CancellationToken cancellationToken = default);
    Task<UserViewModel> UpdateUserAsync(int id, UserUpdateDTO dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteUserAsync(int id, CancellationToken cancellationToken = default);
}
