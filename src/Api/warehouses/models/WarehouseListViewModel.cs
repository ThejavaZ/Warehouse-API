namespace Api.Warehouses;

public class WarehouseListViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? Name { get; set; }

    public string? Location { get; set; }

    public bool IsActive { get; set; }
}
