namespace WMS.Application.Warehouses;

public class WarehouseViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? Name { get; set; }

    public string? Location { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
