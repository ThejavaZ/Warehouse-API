using WMS.Application.Users;

namespace Api.Auth;

public interface IAuthService
{
    Task<AuthViewModel> LoginAsync(LoginDTO dto, CancellationToken cancellationToken = default);
    Task<AuthViewModel> RefreshAsync(RefreshTokenDTO dto, CancellationToken cancellationToken = default);
    Task RevokeAsync(RefreshTokenDTO dto, CancellationToken cancellationToken = default);
    Task<UserViewModel> RegisterAsync(UserCreateDTO dto, CancellationToken cancellationToken = default);
}
