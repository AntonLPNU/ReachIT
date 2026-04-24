// Exposes editable app settings placeholders.
namespace ReachIT.Presentation.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool _showRelatedTasksInTree;

    public bool ShowRelatedTasksInTree
    {
        get => _showRelatedTasksInTree;
        set => SetProperty(ref _showRelatedTasksInTree, value);
    }
}
