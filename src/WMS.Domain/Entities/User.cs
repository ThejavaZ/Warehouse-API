using System.ComponentModel.DataAnnotations.Schema;
using WMS.Domain.Common;

namespace WMS.Domain.Entities;

[Table("users")]
public class User : Model
{
    public string? Email { get; set; }
    public string Password { get; set; } = null!;
}