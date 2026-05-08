using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public class AccountSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public SubscriptionPlanType PlanType { get; set; } = SubscriptionPlanType.Free;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Inactive;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CurrentPeriodEndsAt { get; set; }
    public DateTime? TrialEndsAt { get; set; }
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string ExternalSubscriptionId { get; set; } = string.Empty;
    public string EntitlementsOverrideSerialized { get; set; } = string.Empty;
}
