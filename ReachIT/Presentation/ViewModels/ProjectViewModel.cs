// Represents current project metadata in presentation layer.
namespace ReachIT.Presentation.ViewModels;

public sealed class ProjectViewModel : ViewModelBase
{
    private string _projectName = "ReachIT Project";
    private string _ritPath = "ProjectName.rit";

    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    public string RitPath
    {
        get => _ritPath;
        set => SetProperty(ref _ritPath, value);
    }
}
