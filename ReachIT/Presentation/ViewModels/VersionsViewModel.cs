// Placeholder version control workspace view model.
using System.Collections.ObjectModel;

namespace ReachIT.Presentation.ViewModels;

public sealed class VersionsViewModel : ViewModelBase
{
    public ObservableCollection<string> ProjectVersions { get; } = new(["v0.1-initial"]);
    public ObservableCollection<string> FileVersions { get; } = new(["Main.cs: v0.1"]);

    public void RollbackProject()
    {
        // TODO: Implement safe rollback for project versions.
    }

    public void RollbackFile()
    {
        // TODO: Implement safe rollback for file versions.
    }
}
