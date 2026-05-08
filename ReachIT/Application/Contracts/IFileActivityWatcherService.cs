using ReachIT.Domain.Models;

namespace ReachIT.Application.Contracts;

public interface IFileActivityWatcherService : IDisposable
{
    event EventHandler<ActivityEvent>? ActivityDetected;
    void Start(ProjectMeta project, IReadOnlyCollection<string> ignoredFolders, bool trackTextStatistics);
    void Stop();
}
