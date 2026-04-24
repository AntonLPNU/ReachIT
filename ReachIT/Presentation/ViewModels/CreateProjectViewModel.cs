// Handles create-project form actions and validation.
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class CreateProjectViewModel : ViewModelBase
{
    private readonly IProjectService _projectService;
    private readonly IDialogService _dialogService;
    private string _projectName = string.Empty;
    private string _description = string.Empty;
    private string _saveLocation = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private ProjectTemplateType _selectedTemplate = ProjectTemplateType.EmptyProject;
    private readonly AsyncCommand _createCommand;

    public CreateProjectViewModel(IProjectService projectService, IDialogService dialogService)
    {
        _projectService = projectService;
        _dialogService = dialogService;

        Templates = new ObservableCollection<ProjectTemplateType>(Enum.GetValues<ProjectTemplateType>());

        _createCommand = new AsyncCommand(_ => CreateAsync(), _ => CanCreateProject());
        CreateCommand = _createCommand;
        CancelCommand = new RelayCommand(_ => RequestCancel?.Invoke(this, EventArgs.Empty));
        BrowseLocationCommand = new RelayCommand(_ => BrowseLocation());
        AddInitialExternalFileCommand = new RelayCommand(_ => AddInitialExternalFile());
    }

    public ObservableCollection<ProjectTemplateType> Templates { get; }
    public ObservableCollection<string> InitialExternalFiles { get; } = new();

    public string ProjectName
    {
        get => _projectName;
        set
        {
            if (SetProperty(ref _projectName, value))
            {
                _createCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string SaveLocation
    {
        get => _saveLocation;
        set
        {
            if (SetProperty(ref _saveLocation, value))
            {
                _createCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ProjectTemplateType SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string? CreatedProjectFolderPath { get; private set; }

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseLocationCommand { get; }
    public ICommand AddInitialExternalFileCommand { get; }

    public event EventHandler<string>? RequestCreated;
    public event EventHandler? RequestCancel;

    private async Task CreateAsync()
    {
        var request = new CreateProjectRequest
        {
            ProjectName = ProjectName.Trim(),
            Description = Description.Trim(),
            SaveLocation = SaveLocation.Trim(),
            TemplateType = SelectedTemplate,
            InitialExternalFiles = InitialExternalFiles.ToList()
        };

        var project = await _projectService.CreateProjectAsync(request).ConfigureAwait(true);
        CreatedProjectFolderPath = project.ProjectDirectoryPath;
        RequestCreated?.Invoke(this, project.ProjectDirectoryPath);
    }

    private void BrowseLocation()
    {
        var selectedDirectory = _dialogService.ShowOpenFolderDialog(SaveLocation);
        if (!string.IsNullOrWhiteSpace(selectedDirectory))
        {
            SaveLocation = selectedDirectory;
        }
    }

    private void AddInitialExternalFile()
    {
        var path = _dialogService.ShowOpenFileDialog("All Files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(path))
        {
            InitialExternalFiles.Add(path);
        }
    }

    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectName) && !string.IsNullOrWhiteSpace(SaveLocation);
    }
}
