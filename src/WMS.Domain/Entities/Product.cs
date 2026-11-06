using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WMS.Domain.Common;

namespace WMS.Domain.Entities;

[Table("products")]
public class Product : Model
{
    [MaxLength(50)]
    public string SKU { get; set; } = null!;
    public decimal Price { get; set; }

    public Product()
    {
        if (Price < 0) throw new ArgumentException("Price has not minimal to 0");
    }
}
