// Represents a local user profile for account-aware app behavior.
namespace ReachIT.Domain.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = "Local User";
    public string LoginName { get; set; } = "local";
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = "Local User";
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeveloperAccount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
