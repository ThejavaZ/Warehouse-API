using System.ComponentModel.DataAnnotations.Schema;
using WMS.Domain.Common;

namespace Domain.Entities;

[Table("warehouses")]
public class Warehouse : Model
{
    public string? Location { get; set; }
}