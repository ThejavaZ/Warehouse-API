using Api.Common;

namespace Api.Users;

public class UserQuery : PagedQuery
{
    public int? Id { get; set; }

    public string? Search { get; set; }

    public bool? IsActive { get; set; }

}