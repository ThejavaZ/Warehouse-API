using Api.Users;
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
    }
}
