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
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.UUID).IsRequired();
            entity.Property(u => u.Name).HasMaxLength(50).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Password).HasMaxLength(100).IsRequired();
            entity.Property(u => u.IsActive).IsRequired();
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.UpdatedAt).IsRequired();
            entity.Property(u => u.SoftDelete);

            entity.HasIndex(u => u.UUID).IsUnique();

            // Índice único SOLO sobre usuarios no eliminados lógicamente: permite
            // reutilizar un correo de una cuenta dada de baja sin violar la unicidad.
            entity.HasIndex(u => u.Email)
                .IsUnique()
                .HasFilter("\"SoftDelete\" IS NULL");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);

            entity.Property(rt => rt.Token).HasMaxLength(64).IsRequired();
            entity.Property(rt => rt.ExpiresAt).IsRequired();
            entity.Property(rt => rt.CreatedAt).IsRequired();
            entity.Property(rt => rt.ReplacedByToken).HasMaxLength(64);

            // Se busca por hash, nunca por el valor en claro.
            entity.HasIndex(rt => rt.Token).IsUnique();

            // Baja física de un usuario → sus sesiones desaparecen con él.
            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.UUID).IsRequired();
            entity.Property(p => p.SKU).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(500);
            entity.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
            entity.Property(p => p.IsActive).IsRequired();
            entity.Property(p => p.CreatedAt).IsRequired();
            entity.Property(p => p.UpdatedAt).IsRequired();
            entity.Property(p => p.SoftDelete);

            entity.HasIndex(p => p.UUID).IsUnique();

            // Mismo criterio que Email en Users: SKU único solo entre productos no dados de baja.
            entity.HasIndex(p => p.SKU)
                .IsUnique()
                .HasFilter("\"SoftDelete\" IS NULL");
        });

        modelBuilder.Entity<Warehouse>(entity =>
        {
            entity.HasKey(w => w.Id);

            entity.Property(w => w.UUID).IsRequired();
            entity.Property(w => w.Name).HasMaxLength(100).IsRequired();
            entity.Property(w => w.Location).HasMaxLength(200);
            entity.Property(w => w.IsActive).IsRequired();
            entity.Property(w => w.CreatedAt).IsRequired();
            entity.Property(w => w.UpdatedAt).IsRequired();
            entity.Property(w => w.SoftDelete);

            entity.HasIndex(w => w.UUID).IsUnique();

            // Nombre único solo entre almacenes no dados de baja (mismo patrón que Email/SKU).
            entity.HasIndex(w => w.Name)
                .IsUnique()
                .HasFilter("\"SoftDelete\" IS NULL");
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.Quantity).IsRequired();
            entity.Property(i => i.CreatedAt).IsRequired();
            entity.Property(i => i.UpdatedAt).IsRequired();

            // Una ficha por par almacén–producto: es la garantía que permite el upsert de restock.
            entity.HasIndex(i => new { i.WarehouseId, i.ProductId }).IsUnique();

            // Regla "nunca negativo" también en la propia BD, además de en la validación.
            entity.ToTable("inventory_items", table =>
                table.HasCheckConstraint("CK_inventory_items_Quantity", "\"Quantity\" >= 0"));

            // Restrict en ambos: no se puede borrar físicamente un almacén o producto
            // que todavía tenga existencias asociadas (los borrados del negocio son lógicos).
            entity.HasOne(i => i.Warehouse)
                .WithMany()
                .HasForeignKey(i => i.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
