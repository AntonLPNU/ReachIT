// Placeholder file workspace view model.
namespace ReachIT.Presentation.ViewModels;

public sealed class FileViewModel : ViewModelBase
{
    private string _selectedNodeName = "Files";
    private string _selectedRelativePath = string.Empty;
    private string _selectedFullPath = string.Empty;

    public string SelectedNodeName
    {
        get => _selectedNodeName;
        set => SetProperty(ref _selectedNodeName, value);
    }

    public string SelectedRelativePath
    {
        get => _selectedRelativePath;
        set => SetProperty(ref _selectedRelativePath, value);
    }

    public string SelectedFullPath
    {
        get => _selectedFullPath;
        set => SetProperty(ref _selectedFullPath, value);
    }
}
