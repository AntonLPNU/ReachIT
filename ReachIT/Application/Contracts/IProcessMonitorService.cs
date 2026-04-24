// Defines safe process-monitoring contracts for future focus mode rules.
namespace ReachIT.Application.Contracts;

public interface IProcessMonitorService
{
    Task<IReadOnlyList<string>> GetRunningProcessNamesAsync(CancellationToken cancellationToken = default);
}
