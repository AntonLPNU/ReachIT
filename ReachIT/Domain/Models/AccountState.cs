namespace ReachIT.Domain.Models;

public sealed class AccountState
{
    public required User User { get; init; }
    public required AccountSubscription Subscription { get; init; }
    public IReadOnlyList<FeatureAccess> Features { get; init; } = [];
}
