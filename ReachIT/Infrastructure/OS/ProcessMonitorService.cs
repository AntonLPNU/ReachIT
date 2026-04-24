// Reads running process names for future focus mode checks.
using System.Diagnostics;
using ReachIT.Application.Contracts;

namespace ReachIT.Infrastructure.OS;

public sealed class ProcessMonitorService : IProcessMonitorService
{
    public Task<IReadOnlyList<string>> GetRunningProcessNamesAsync(CancellationToken cancellationToken = default)
    {
        var processes = Process.GetProcesses()
            .Select(p => p.ProcessName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(processes);
    }
}
