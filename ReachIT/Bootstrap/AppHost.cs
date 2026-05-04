// Composes and provides application-level services and view models.
using ReachIT.Application.Contracts;
using ReachIT.Application.Services;
using ReachIT.Infrastructure.OS;
using ReachIT.Infrastructure.Persistence;
using ReachIT.Presentation.Services;
using ReachIT.Presentation.ViewModels;

namespace ReachIT.Bootstrap;

public sealed class AppHost
{
    private readonly Dictionary<Type, object> _services = new();

    public void Initialize()
    {
        RegisterSingleton<ReachIT.Infrastructure.Logging.ILocalLogger>(new ReachIT.Infrastructure.Logging.LocalLogger());
        RegisterSingleton<IDatabaseService>(new DatabaseService());
        RegisterSingleton<IAppSettingsService>(new AppSettingsService(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IProcessMonitorService>(new ProcessMonitorService());
        RegisterSingleton<IDialogService>(new DialogService());
        RegisterSingleton<IOverlayService>(new OverlayService());
        RegisterSingleton<INavigationService>(new NavigationService());
        RegisterSingleton<ITrayIconService>(new TrayIconService());
        RegisterSingleton<IGlobalHotkeyService>(new GlobalHotkeyService(GetRequiredService<ReachIT.Infrastructure.Logging.ILocalLogger>()));
        RegisterSingleton<IRecentFilesService>(new RecentFilesService(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IWorkItemRepository>(new WorkItemRepository(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IWorkUnitRepository>(new WorkUnitRepository(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IExternalResourceService>(new ExternalResourceService(
            GetRequiredService<IDatabaseService>(),
            GetRequiredService<IRecentFilesService>()));
        RegisterSingleton<IFileSystemProjectExplorerService>(new FileSystemProjectExplorerService(
            GetRequiredService<IExternalResourceService>()));

        RegisterSingleton<IProjectService>(new ProjectService(
            GetRequiredService<IDialogService>(),
            GetRequiredService<IRecentFilesService>(),
            GetRequiredService<IDatabaseService>(),
            GetRequiredService<IFileSystemProjectExplorerService>()));
        RegisterSingleton<ITaskService>(new TaskService(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IWorkItemService>(new WorkItemService(
            GetRequiredService<IWorkItemRepository>(),
            GetRequiredService<ITaskService>()));
        RegisterSingleton<IWorkUnitService>(new WorkUnitService(
            GetRequiredService<IWorkUnitRepository>(),
            GetRequiredService<IWorkItemRepository>()));
        RegisterSingleton<IProgressCalculationService>(new ProgressCalculationService(GetRequiredService<IWorkItemRepository>()));
        RegisterSingleton<ITaskSuggestionService>(new TaskSuggestionService(
            GetRequiredService<IDatabaseService>(),
            GetRequiredService<IWorkItemService>()));
        RegisterSingleton<ITaskLinkingService>(new TaskLinkingService(GetRequiredService<IWorkItemRepository>()));
        RegisterSingleton<IProjectProgressService>(new ProjectProgressService(
            GetRequiredService<IDatabaseService>(),
            GetRequiredService<IWorkItemService>(),
            GetRequiredService<IProgressCalculationService>(),
            GetRequiredService<ITaskSuggestionService>()));
        RegisterSingleton<IStatisticsService>(new StatisticsService(GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IFocusModeService>(new FocusModeService(GetRequiredService<IProcessMonitorService>(), GetRequiredService<IDatabaseService>()));
        RegisterSingleton<IGitService>(new GitService());

        RegisterSingleton(new ProjectTreeViewModel());
        RegisterSingleton(new RecentExternalFilesPanelViewModel(
            GetRequiredService<IRecentFilesService>(),
            GetRequiredService<IExternalResourceService>(),
            GetRequiredService<IProjectService>()));
        RegisterSingleton(new MainDashboardViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<ITaskService>(),
            GetRequiredService<IStatisticsService>(),
            GetRequiredService<IExternalResourceService>(),
            GetRequiredService<IFocusModeService>(),
            GetRequiredService<IProjectProgressService>()));
        RegisterSingleton(new ProjectInfoViewModel());
        RegisterSingleton(new FileViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<ITaskService>()));
        RegisterSingleton(new PlanningViewModel(GetRequiredService<ITaskService>()));
        RegisterSingleton(new VersionsViewModel(GetRequiredService<IDatabaseService>()));
        RegisterSingleton(new TaskManagerViewModel(
            GetRequiredService<ITaskService>(),
            GetRequiredService<IProjectService>(),
            GetRequiredService<IGitService>()));
        RegisterSingleton(new TaskDetailViewModel());
        RegisterSingleton(new StatisticsViewModel(GetRequiredService<IStatisticsService>()));
        RegisterSingleton(new SettingsViewModel(GetRequiredService<IDatabaseService>()));
        RegisterSingleton(new FocusModeViewModel(GetRequiredService<IFocusModeService>(), GetRequiredService<IOverlayService>()));
        RegisterSingleton(new OverlayViewModel());
        RegisterSingleton(new CreateProjectViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IDialogService>()));
        RegisterSingleton(new StartViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IRecentFilesService>()));
        RegisterSingleton(new FloatingLogoViewModel());
        RegisterSingleton(new QuickMenuViewModel());
        RegisterSingleton(new QuickAddTaskViewModel(
            GetRequiredService<ITaskService>(),
            GetRequiredService<IRecentFilesService>()));

        RegisterSingleton(new MainViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IExternalResourceService>(),
            GetRequiredService<IFileSystemProjectExplorerService>(),
            GetRequiredService<IWorkUnitService>(),
            GetRequiredService<INavigationService>(),
            GetRequiredService<IDialogService>(),
            GetRequiredService<ProjectTreeViewModel>(),
            GetRequiredService<RecentExternalFilesPanelViewModel>(),
            GetRequiredService<MainDashboardViewModel>(),
            GetRequiredService<ProjectInfoViewModel>(),
            GetRequiredService<FileViewModel>(),
            GetRequiredService<TaskManagerViewModel>(),
            GetRequiredService<StatisticsViewModel>(),
            GetRequiredService<PlanningViewModel>(),
            GetRequiredService<VersionsViewModel>(),
            GetRequiredService<SettingsViewModel>(),
            GetRequiredService<FocusModeViewModel>()));

        RegisterSingleton<IWindowManagerService>(new WindowManagerService(
            GetRequiredService<IAppSettingsService>(),
            GetRequiredService<IRecentFilesService>(),
            GetRequiredService<IProjectService>(),
            GetRequiredService<ITrayIconService>(),
            GetRequiredService<IGlobalHotkeyService>(),
            GetRequiredService<ReachIT.Infrastructure.Logging.ILocalLogger>(),
            GetRequiredService<MainViewModel>(),
            GetRequiredService<FloatingLogoViewModel>(),
            GetRequiredService<QuickMenuViewModel>(),
            GetRequiredService<QuickAddTaskViewModel>()));
    }

    public T GetRequiredService<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var service) && service is T typedService)
        {
            return typedService;
        }

        throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
    }

    private void RegisterSingleton<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance;
    }
}
