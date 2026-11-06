namespace WMS.Domain.Entities;

public class Inventory
{
    public string? Fole { get; set; }
    public DateTime Datetime { get; set; } = DateTime.UtcNow;
}
