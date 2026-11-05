using Api.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Data.Configurations;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Quantity).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // Una ficha por par almacén–producto: es la garantía que permite el upsert de restock.
        builder.HasIndex(i => new { i.WarehouseId, i.ProductId }).IsUnique();

        // Regla "nunca negativo" también en la propia BD, además de en la validación.
        builder.ToTable("inventory_items", table =>
            table.HasCheckConstraint("CK_inventory_items_Quantity", "\"Quantity\" >= 0"));

        // Restrict en ambos: no se puede borrar físicamente un almacén o producto
        // que todavía tenga existencias asociadas (los borrados del negocio son lógicos).
        builder.HasOne(i => i.Warehouse)
            .WithMany()
            .HasForeignKey(i => i.WarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
