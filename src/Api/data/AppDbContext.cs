using Api.Auth;
using Api.Inventory;
using Api.Products;
using Api.Users;
using Api.Warehouses;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Oculta intencionalmente el DbSet<IdentityUser> Users heredado de IdentityDbContext:
    // la entidad de dominio de la aplicación es Api.Users.User, no IdentityUser.
    public new DbSet<User> Users { get; set; } = null!;

    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    public DbSet<Product> Products { get; set; } = null!;

    public DbSet<Warehouse> Warehouses { get; set; } = null!;

    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity (AspNetUsers/AspNetRoles…) se configura aquí; el resto de
        // entidades, en sus IEntityTypeConfiguration de Api.Data.Configurations.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
