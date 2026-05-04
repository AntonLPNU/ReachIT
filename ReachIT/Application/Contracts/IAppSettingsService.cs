using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IAppSettingsService
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
