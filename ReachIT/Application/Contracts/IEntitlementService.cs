using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IEntitlementService
{
    Task<IReadOnlyList<FeatureAccess>> GetFeatureAccessAsync(CancellationToken cancellationToken = default);
    Task<FeatureAccess> GetFeatureAccessAsync(AccountFeature feature, CancellationToken cancellationToken = default);
    Task<bool> IsEnabledAsync(AccountFeature feature, CancellationToken cancellationToken = default);
}
