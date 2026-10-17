using Api.Security;
using Api.Users;
using Microsoft.AspNetCore.Identity;

namespace Api.Config;

public static class ServicesConfiguration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        // BCrypt implementado sobre la abstracción IPasswordHasher<TUser> de ASP.NET Core Identity.
        // (AddIdentity solo registra IPasswordHasher<IdentityUser>, no IPasswordHasher<User>.)
        services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();

        return services;
    }
}