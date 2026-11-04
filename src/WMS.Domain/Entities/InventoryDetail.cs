namespace WMS.Domain.Entities;

public class InventoryDetail
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
}