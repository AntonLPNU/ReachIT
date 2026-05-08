using Microsoft.EntityFrameworkCore;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using System.Security.Cryptography;
using System.Text;

namespace ReachIT.Application.Services;

public sealed class AccountService : IAccountService
{
    public const string DeveloperLogin = "developer";
    public const string DeveloperPassword = "ReachIT-dev-2026!";

    private readonly IDatabaseService _databaseService;
    private readonly Lazy<IEntitlementService> _entitlementService;

    public AccountService(IDatabaseService databaseService, Lazy<IEntitlementService> entitlementService)
    {
        _databaseService = databaseService;
        _entitlementService = entitlementService;
    }

    public async Task<AccountState> GetAccountStateAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetOrCreateLocalUserAsync(cancellationToken).ConfigureAwait(false);
        var subscription = await GetSubscriptionAsync(user.Id, cancellationToken).ConfigureAwait(false);
        var features = await _entitlementService.Value.GetFeatureAccessAsync(cancellationToken).ConfigureAwait(false);

        return new AccountState
        {
            User = user,
            Subscription = subscription,
            Features = features
        };
    }

    public async Task<User> GetOrCreateLocalUserAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDeveloperAccount)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is not null)
        {
            return user;
        }

        user = new User
        {
            UserName = "local",
            LoginName = "local",
            DisplayName = "Local User",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.Users.Add(user);
        db.AccountSubscriptions.Add(CreateDefaultSubscription(user.Id));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<User> EnsureDeveloperAccountAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .FirstOrDefaultAsync(x => x.LoginName == DeveloperLogin, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var hash = HashPassword(DeveloperPassword, salt);

        if (user is null)
        {
            user = new User
            {
                UserName = DeveloperLogin,
                LoginName = DeveloperLogin,
                DisplayName = "ReachIT Developer",
                Email = "developer@reachit.local",
                PasswordSalt = salt,
                PasswordHash = hash,
                IsActive = false,
                IsDeveloperAccount = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            user.UserName = DeveloperLogin;
            user.DisplayName = "ReachIT Developer";
            user.Email = string.IsNullOrWhiteSpace(user.Email) ? "developer@reachit.local" : user.Email;
            user.PasswordSalt = salt;
            user.PasswordHash = hash;
            user.IsDeveloperAccount = true;
            user.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var subscription = await db.AccountSubscriptions
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            subscription = new AccountSubscription
            {
                UserId = user.Id,
                StartedAt = DateTime.UtcNow
            };
            db.AccountSubscriptions.Add(subscription);
        }

        subscription.PlanType = SubscriptionPlanType.Internal;
        subscription.Status = SubscriptionStatus.Active;
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.CurrentPeriodEndsAt = null;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<User?> SignInAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .FirstOrDefaultAsync(x => x.LoginName == loginName, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            return null;
        }

        if (!string.Equals(user.PasswordHash, HashPassword(password, user.PasswordSalt), StringComparison.Ordinal))
        {
            return null;
        }

        var users = await db.Users.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var item in users)
        {
            item.IsActive = item.Id == user.Id;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var users = await db.Users.ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var user in users)
        {
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
        }

        var localUser = users.FirstOrDefault(x => x.LoginName == "local");
        if (localUser is null)
        {
            localUser = new User
            {
                UserName = "local",
                LoginName = "local",
                DisplayName = "Local User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(localUser);
            db.AccountSubscriptions.Add(CreateDefaultSubscription(localUser.Id));
        }
        else
        {
            localUser.IsActive = true;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ValidateCredentialsAsync(string loginName, string password, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .FirstOrDefaultAsync(x => x.LoginName == loginName && x.IsActive, cancellationToken)
            .ConfigureAwait(false);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.PasswordSalt))
        {
            return false;
        }

        return string.Equals(user.PasswordHash, HashPassword(password, user.PasswordSalt), StringComparison.Ordinal);
    }

    public async Task<User> UpdateLocalProfileAsync(string displayName, string email, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDeveloperAccount)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = new User
            {
                UserName = "local",
                LoginName = "local",
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }

        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Local User" : displayName.Trim();
        user.Email = email.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return user;
    }

    public async Task<AccountSubscription> GetSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var subscription = await db.AccountSubscriptions
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subscription is not null)
        {
            return subscription;
        }

        subscription = CreateDefaultSubscription(userId);
        db.AccountSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subscription;
    }

    public async Task<AccountSubscription> SetLocalSubscriptionAsync(
        SubscriptionPlanType planType,
        SubscriptionStatus status,
        CancellationToken cancellationToken = default)
    {
        await using var db = _databaseService.CreateDbContext();
        var user = await db.Users
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.IsDeveloperAccount)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            user = new User
            {
                UserName = "local",
                LoginName = "local",
                DisplayName = "Local User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var subscription = await db.AccountSubscriptions
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            subscription = CreateDefaultSubscription(user.Id);
            db.AccountSubscriptions.Add(subscription);
        }

        subscription.PlanType = planType;
        subscription.Status = planType == SubscriptionPlanType.Free ? SubscriptionStatus.Inactive : status;
        subscription.UpdatedAt = DateTime.UtcNow;
        subscription.CurrentPeriodEndsAt = planType == SubscriptionPlanType.Free ? null : DateTime.UtcNow.AddMonths(1);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return subscription;
    }

    private static AccountSubscription CreateDefaultSubscription(Guid userId)
    {
        return new AccountSubscription
        {
            UserId = userId,
            PlanType = SubscriptionPlanType.Free,
            Status = SubscriptionStatus.Inactive,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string HashPassword(string password, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes($"{salt}:{password}");
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }
}
