namespace WMS.Application.Users;

public class UserViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
