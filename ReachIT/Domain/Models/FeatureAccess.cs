using ReachIT.Domain.Enums;

namespace ReachIT.Domain.Models;

public sealed record FeatureAccess(
    AccountFeature Feature,
    bool IsEnabled,
    string Source,
    string Reason);
