// Represents a local user profile for future multi-user support.
namespace ReachIT.Domain.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "Local User";
    public string Email { get; set; } = string.Empty;
}
