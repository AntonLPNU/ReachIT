// Handles create-project form actions and validation.
using System.Collections.ObjectModel;
using System.IO;
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
    private string _finalGoal = string.Empty;
    private string _mainTopic = string.Empty;
    private string _desiredResult = string.Empty;
    private string _resultFormat = string.Empty;
    private string _knownSections = string.Empty;
    private string _detailLevel = "Medium";
    private DateTime? _deadlineDate;
    private bool _createStarterFiles = true;
    private bool _linkTasksToFiles = true;
    private readonly AsyncCommand _createCommand;

    public CreateProjectViewModel(IProjectService projectService, IDialogService dialogService)
    {
        _projectService = projectService;
        _dialogService = dialogService;

        Templates = new ObservableCollection<ProjectTemplateType>(Enum.GetValues<ProjectTemplateType>());
        DetailLevels = new ObservableCollection<string>(["Minimal", "Medium", "Detailed"]);

        _createCommand = new AsyncCommand(_ => CreateAsync(), _ => CanCreateProject());
        CreateCommand = _createCommand;
        CancelCommand = new RelayCommand(_ => RequestCancel?.Invoke(this, EventArgs.Empty));
        AutofillCommand = new RelayCommand(_ => AutofillForTesting());
        BrowseLocationCommand = new RelayCommand(_ => BrowseLocation());
        AddInitialExternalFileCommand = new RelayCommand(_ => AddInitialExternalFile());
    }

    public ObservableCollection<ProjectTemplateType> Templates { get; }
    public ObservableCollection<string> DetailLevels { get; }
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
        set
        {
            if (SetProperty(ref _selectedTemplate, value))
            {
                OnPropertyChanged(nameof(TemplateHelpText));
                OnPropertyChanged(nameof(MainTopicLabel));
                OnPropertyChanged(nameof(ResultFormatLabel));
                OnPropertyChanged(nameof(KnownSectionsLabel));
                ApplyTemplateDefaults();
            }
        }
    }

    public string FinalGoal
    {
        get => _finalGoal;
        set => SetProperty(ref _finalGoal, value);
    }

    public string MainTopic
    {
        get => _mainTopic;
        set => SetProperty(ref _mainTopic, value);
    }

    public string DesiredResult
    {
        get => _desiredResult;
        set => SetProperty(ref _desiredResult, value);
    }

    public string ResultFormat
    {
        get => _resultFormat;
        set => SetProperty(ref _resultFormat, value);
    }

    public string KnownSections
    {
        get => _knownSections;
        set => SetProperty(ref _knownSections, value);
    }

    public string DetailLevel
    {
        get => _detailLevel;
        set => SetProperty(ref _detailLevel, value);
    }

    public DateTime? DeadlineDate
    {
        get => _deadlineDate;
        set => SetProperty(ref _deadlineDate, value);
    }

    public bool CreateStarterFiles
    {
        get => _createStarterFiles;
        set => SetProperty(ref _createStarterFiles, value);
    }

    public bool LinkTasksToFiles
    {
        get => _linkTasksToFiles;
        set => SetProperty(ref _linkTasksToFiles, value);
    }

    public string TemplateHelpText => SelectedTemplate switch
    {
        ProjectTemplateType.StudyProject => "For labs, courses, exam prep, notes, and self-learning. It starts from requirements, topic breakdown, materials, main work, and final check.",
        ProjectTemplateType.FreelanceProject => "For client work. It keeps requirements, references, review rounds, revisions, and final delivery visible.",
        ProjectTemplateType.CreativeProject => "For games, books, videos, mods, art, design, music, and other creative work. It turns an idea into a draft and then into a finished result.",
        ProjectTemplateType.ResearchProject => "For analysis, comparison, investigation, and reports. It moves from a question to sources, analysis, and a clear conclusion.",
        _ => "A mostly empty project with one gentle planning task so the screen is not blank."
    };

    public string MainTopicLabel => SelectedTemplate switch
    {
        ProjectTemplateType.StudyProject => "Subject / topic",
        ProjectTemplateType.FreelanceProject => "Client / order",
        ProjectTemplateType.CreativeProject => "Type / idea",
        ProjectTemplateType.ResearchProject => "Research question",
        _ => "Main direction"
    };

    public string ResultFormatLabel => SelectedTemplate switch
    {
        ProjectTemplateType.StudyProject => "Result format",
        ProjectTemplateType.FreelanceProject => "Delivery format",
        ProjectTemplateType.CreativeProject => "Final format",
        ProjectTemplateType.ResearchProject => "Research output",
        _ => "Output format"
    };

    public string KnownSectionsLabel => SelectedTemplate switch
    {
        ProjectTemplateType.StudyProject => "Known subtopics",
        ProjectTemplateType.FreelanceProject => "Known requirements",
        ProjectTemplateType.CreativeProject => "Known parts",
        ProjectTemplateType.ResearchProject => "Known directions",
        _ => "Known stages"
    };

    public string? CreatedProjectFolderPath { get; private set; }

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AutofillCommand { get; }
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
            FinalGoal = FinalGoal.Trim(),
            MainTopic = MainTopic.Trim(),
            DesiredResult = DesiredResult.Trim(),
            ResultFormat = ResultFormat.Trim(),
            KnownSections = KnownSections.Trim(),
            DetailLevel = DetailLevel,
            DeadlineDate = DeadlineDate,
            CreateStarterFiles = CreateStarterFiles,
            LinkTasksToFiles = LinkTasksToFiles,
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

    private void AutofillForTesting()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "ReachIT-TestProjects");

        ProjectName = $"ReachIT Test Project {DateTime.Now:yyyyMMdd-HHmm}";
        Description = "Autofilled project for testing ReachIT planning, files, activity tracking, developer tools, and format catalog workflows.";
        SaveLocation = root;
        SelectedTemplate = ProjectTemplateType.ResearchProject;
        FinalGoal = "Create a realistic test workspace that exercises every major ReachIT feature.";
        MainTopic = "Testing a complex project with documents, code, media, data, configs, and activity events";
        DesiredResult = "A generated workspace with tasks, linked files, readable previews, and enough file variety to validate UI behavior.";
        ResultFormat = "ReachIT test project with format catalog";
        KnownSections = string.Join(Environment.NewLine,
        [
            "Documents and text extraction",
            "Spreadsheets and structured data",
            "Code and configuration files",
            "Images and design assets",
            "3D models and media placeholders",
            "Activity tracking and task suggestions",
            "Project dashboard and statistics"
        ]);
        DetailLevel = "Detailed";
        DeadlineDate = DateTime.Today.AddDays(14);
        CreateStarterFiles = true;
        LinkTasksToFiles = true;
        _createCommand.RaiseCanExecuteChanged();
    }

    private void ApplyTemplateDefaults()
    {
        if (string.IsNullOrWhiteSpace(ResultFormat))
        {
            ResultFormat = SelectedTemplate switch
            {
                ProjectTemplateType.StudyProject => "Notes / final document",
                ProjectTemplateType.FreelanceProject => "Client delivery",
                ProjectTemplateType.CreativeProject => "Finished creative product",
                ProjectTemplateType.ResearchProject => "Report / conclusion",
                _ => string.Empty
            };
        }
    }
}
