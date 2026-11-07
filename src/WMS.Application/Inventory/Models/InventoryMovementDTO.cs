using System.ComponentModel.DataAnnotations;

namespace WMS.Application.Inventory;

/// <summary>Movimiento de entrada (restock) o salida (consume) de inventario.</summary>
public class InventoryMovementDTO
{
    [Required(ErrorMessage = "El almacén es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El almacén debe ser un identificador positivo.")]
    public int WarehouseId { get; set; }

    [Required(ErrorMessage = "El producto es obligatorio.")]
    [Range(1, int.MaxValue, ErrorMessage = "El producto debe ser un identificador positivo.")]
    public int ProductId { get; set; }

    [Required(ErrorMessage = "La cantidad es obligatoria.")]
    [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
    public int Quantity { get; set; }
}
