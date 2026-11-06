using System.ComponentModel.DataAnnotations;

namespace WMS.Application.Products;

public class ProductCreateDTO
{
    [Required(ErrorMessage = "El SKU es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El SKU debe tener entre 2 y 50 caracteres.")]
    public string SKU { get; set; } = null!;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres.")]
    public string Name { get; set; } = null!;

    [StringLength(500, ErrorMessage = "La descripción no puede superar los 500 caracteres.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "El precio es obligatorio.")]
    [Range(0, 999999999, ErrorMessage = "El precio debe ser un valor positivo.")]
    public decimal Price { get; set; }
}
