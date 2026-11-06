using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WMS.Domain.Entities;

namespace WMS.Infrastructure.Persistence.Configurations;

public sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.HasKey(w => w.Id);

        builder.Property(w => w.UUID).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(100).IsRequired();
        builder.Property(w => w.Location).HasMaxLength(200);
        builder.Property(w => w.IsActive).IsRequired();
        builder.Property(w => w.CreatedAt).IsRequired();
        builder.Property(w => w.UpdatedAt).IsRequired();
        builder.Property(w => w.SoftDelete);

        builder.HasIndex(w => w.UUID).IsUnique();

        // Nombre único solo entre almacenes no dados de baja (mismo patrón que Email/SKU).
        builder.HasIndex(w => w.Name)
            .IsUnique()
            .HasFilter("\"SoftDelete\" IS NULL");
    }
}
