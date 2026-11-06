using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Api.Warehouses;
using WMS.Domain.Entities;

namespace Api.Inventory;

/// <summary>
/// Tabla puente explícita almacén–producto (decisión del Sprint 14): permite llevar
/// la cantidad y la auditoría por par, en lugar de un many-to-many oculto.
/// </summary>
[Table("inventory_items")]
public class InventoryItem
{
    [Key]
    public int Id { get; set; }

    public int WarehouseId { get; set; }

    public Warehouse Warehouse { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    /// <summary>Stock actual; nunca negativo (CHECK en BD + validación condicional al consumir).</summary>
    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
