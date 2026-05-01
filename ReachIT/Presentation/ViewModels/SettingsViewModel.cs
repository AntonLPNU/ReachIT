// Exposes editable app settings placeholders.
namespace ReachIT.Presentation.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private bool _showRelatedTasksInTree;
    private bool _hideSidePanelAfterExternalFileOpen = true;

    public bool ShowRelatedTasksInTree
    {
        get => _showRelatedTasksInTree;
        set => SetProperty(ref _showRelatedTasksInTree, value);
    }

    public bool HideSidePanelAfterExternalFileOpen
    {
        get => _hideSidePanelAfterExternalFileOpen;
        set => SetProperty(ref _hideSidePanelAfterExternalFileOpen, value);
    }
}
