namespace Api.Users;

public class UserListViewModel
{
    public int Id { get; set; }

    public Guid UUID { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; }
}
