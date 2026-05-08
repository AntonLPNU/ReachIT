using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IAccountService
{
    Task<AccountState> GetAccountStateAsync(CancellationToken cancellationToken = default);
    Task<User> GetOrCreateLocalUserAsync(CancellationToken cancellationToken = default);
    Task<User> EnsureDeveloperAccountAsync(CancellationToken cancellationToken = default);
    Task<bool> ValidateCredentialsAsync(string loginName, string password, CancellationToken cancellationToken = default);
    Task<User?> SignInAsync(string loginName, string password, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
    Task<User> UpdateLocalProfileAsync(string displayName, string email, CancellationToken cancellationToken = default);
    Task<AccountSubscription> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccountSubscription> SetLocalSubscriptionAsync(SubscriptionPlanType planType, SubscriptionStatus status, CancellationToken cancellationToken = default);
}
