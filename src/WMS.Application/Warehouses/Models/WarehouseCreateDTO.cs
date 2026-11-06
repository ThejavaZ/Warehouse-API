using System.ComponentModel.DataAnnotations;

namespace WMS.Application.Warehouses;

public class WarehouseCreateDTO
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Name { get; set; } = null!;

    [StringLength(200, ErrorMessage = "La ubicación no puede superar los 200 caracteres.")]
    public string? Location { get; set; }
}
