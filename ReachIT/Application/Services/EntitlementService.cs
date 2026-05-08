using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Services;

public sealed class EntitlementService : IEntitlementService
{
    private static readonly IReadOnlyDictionary<SubscriptionPlanType, AccountFeature[]> PlanFeatures =
        new Dictionary<SubscriptionPlanType, AccountFeature[]>
        {
            [SubscriptionPlanType.Free] =
            [
                AccountFeature.CoreWorkspace,
                AccountFeature.ProjectTemplates,
                AccountFeature.FocusMode,
                AccountFeature.ExternalResources,
                AccountFeature.VersionHistory
            ],
            [SubscriptionPlanType.Pro] =
            [
                AccountFeature.CoreWorkspace,
                AccountFeature.ProjectTemplates,
                AccountFeature.FocusMode,
                AccountFeature.ActivityTracking,
                AccountFeature.ActivityDashboard,
                AccountFeature.TaskSuggestions,
                AccountFeature.ExternalResources,
                AccountFeature.VersionHistory,
                AccountFeature.AutomationRules
            ],
            [SubscriptionPlanType.Team] =
            [
                AccountFeature.CoreWorkspace,
                AccountFeature.ProjectTemplates,
                AccountFeature.FocusMode,
                AccountFeature.ActivityTracking,
                AccountFeature.ActivityDashboard,
                AccountFeature.TaskSuggestions,
                AccountFeature.ExternalResources,
                AccountFeature.VersionHistory,
                AccountFeature.CloudSync,
                AccountFeature.TeamWorkspaces,
                AccountFeature.AutomationRules
            ],
            [SubscriptionPlanType.Internal] = Enum.GetValues<AccountFeature>()
        };

    private readonly IAccountService _accountService;

    public EntitlementService(IAccountService accountService)
    {
        _accountService = accountService;
    }

    public async Task<IReadOnlyList<FeatureAccess>> GetFeatureAccessAsync(CancellationToken cancellationToken = default)
    {
        var user = await _accountService.GetOrCreateLocalUserAsync(cancellationToken).ConfigureAwait(false);
        var subscription = await _accountService.GetSubscriptionAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var enabledFeatures = GetEnabledFeatures(subscription);

        return Enum.GetValues<AccountFeature>()
            .Select(feature => CreateAccess(feature, subscription, enabledFeatures))
            .ToList();
    }

    public async Task<FeatureAccess> GetFeatureAccessAsync(AccountFeature feature, CancellationToken cancellationToken = default)
    {
        var features = await GetFeatureAccessAsync(cancellationToken).ConfigureAwait(false);
        return features.First(x => x.Feature == feature);
    }

    public async Task<bool> IsEnabledAsync(AccountFeature feature, CancellationToken cancellationToken = default)
    {
        var access = await GetFeatureAccessAsync(feature, cancellationToken).ConfigureAwait(false);
        return access.IsEnabled;
    }

    private static HashSet<AccountFeature> GetEnabledFeatures(AccountSubscription subscription)
    {
        var plan = IsSubscriptionUsable(subscription)
            ? subscription.PlanType
            : SubscriptionPlanType.Free;

        return PlanFeatures.TryGetValue(plan, out var features)
            ? features.ToHashSet()
            : PlanFeatures[SubscriptionPlanType.Free].ToHashSet();
    }

    private static FeatureAccess CreateAccess(
        AccountFeature feature,
        AccountSubscription subscription,
        ISet<AccountFeature> enabledFeatures)
    {
        var isEnabled = enabledFeatures.Contains(feature);
        var plan = IsSubscriptionUsable(subscription)
            ? subscription.PlanType
            : SubscriptionPlanType.Free;

        return new FeatureAccess(
            feature,
            isEnabled,
            plan.ToString(),
            isEnabled
                ? $"Included in {plan}."
                : $"Requires a higher subscription than {plan}.");
    }

    private static bool IsSubscriptionUsable(AccountSubscription subscription)
    {
        if (subscription.PlanType == SubscriptionPlanType.Internal)
        {
            return true;
        }

        if (subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
        {
            return subscription.CurrentPeriodEndsAt is null || subscription.CurrentPeriodEndsAt > DateTime.UtcNow;
        }

        return subscription.PlanType == SubscriptionPlanType.Free;
    }
}
