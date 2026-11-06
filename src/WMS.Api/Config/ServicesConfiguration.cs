using Api.Auth;
using Api.Inventory;
using Api.Products;
using Api.Security;
using Api.Warehouses;
using Microsoft.AspNetCore.Identity;
using WMS.Application.Auth;
using WMS.Application.Users;
using WMS.Domain.Entities;
using WMS.Infrastructure.Persistence.Repositories;

namespace WMS.Api.Config;

public static class ServicesConfiguration
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductService, ProductService>();

        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IWarehouseService, WarehouseService>();

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Autenticación: login contra la entidad User de negocio + refresh tokens rotatorios.
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // BCrypt implementado sobre la abstracción IPasswordHasher<TUser> de ASP.NET Core Identity.
        // (AddIdentity solo registra IPasswordHasher<IdentityUser>, no IPasswordHasher<User>.)
        services.AddScoped<IPasswordHasher<User>, BCryptPasswordHasher>();

        return services;
    }
}
