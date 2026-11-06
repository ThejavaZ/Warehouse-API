using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WMS.Domain.Common;

namespace WMS.Domain.Entities;

[Table("users")]
public class User : Model
{
    [MaxLength(100)]
    public string Email { get; set; } = null!;

    [MaxLength(100)]
    public string Password { get; set; } = null!;

}
