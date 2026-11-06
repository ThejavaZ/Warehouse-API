using Api.Inventory;
using Api.Warehouses;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
// Alias en lugar de un using en bruto: arrastraría los scaffolds de WMS.Domain
// aún sin migrar (Warehouse.cs…) y colisionaría con los tipos de Api.*.
using Product = WMS.Domain.Entities.Product;
using RefreshToken = WMS.Domain.Entities.RefreshToken;
using User = WMS.Domain.Entities.User;

namespace WMS.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    // Oculta intencionalmente el DbSet<IdentityUser> Users heredado de IdentityDbContext:
    // la entidad de dominio de la aplicación es WMS.Domain.Entities.User, no IdentityUser.
    public new DbSet<User> Users { get; set; } = null!;

    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    public DbSet<Product> Products { get; set; } = null!;

    public DbSet<Warehouse> Warehouses { get; set; } = null!;

    public DbSet<InventoryItem> InventoryItems { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity (AspNetUsers/AspNetRoles…) se configura aquí; el resto de
        // entidades, en sus IEntityTypeConfiguration de WMS.Infrastructure.Persistence.Configurations.
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
