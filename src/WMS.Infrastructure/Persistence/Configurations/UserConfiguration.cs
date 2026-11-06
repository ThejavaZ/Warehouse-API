using Api.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WMS.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UUID).IsRequired();
        builder.Property(u => u.Name).HasMaxLength(50).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Password).HasMaxLength(100).IsRequired();
        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();
        builder.Property(u => u.UpdatedAt).IsRequired();
        builder.Property(u => u.SoftDelete);

        builder.HasIndex(u => u.UUID).IsUnique();

        // Índice único SOLO sobre usuarios no eliminados lógicamente: permite
        // reutilizar un correo de una cuenta dada de baja sin violar la unicidad.
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"SoftDelete\" IS NULL");
    }
}
