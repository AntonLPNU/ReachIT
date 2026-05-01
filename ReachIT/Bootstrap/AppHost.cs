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
        RegisterSingleton<IDatabaseService>(new DatabaseService());
        RegisterSingleton<IProcessMonitorService>(new ProcessMonitorService());
        RegisterSingleton<IDialogService>(new DialogService());
        RegisterSingleton<IOverlayService>(new OverlayService());
        RegisterSingleton<INavigationService>(new NavigationService());
        RegisterSingleton<IRecentFilesService>(new RecentFilesService(GetRequiredService<IDatabaseService>()));
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
        RegisterSingleton<ITaskService>(new TaskService());
        RegisterSingleton<IStatisticsService>(new StatisticsService());
        RegisterSingleton<IFocusModeService>(new FocusModeService(GetRequiredService<IProcessMonitorService>()));

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
            GetRequiredService<IFocusModeService>()));
        RegisterSingleton(new ProjectInfoViewModel());
        RegisterSingleton(new FileViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<ITaskService>()));
        RegisterSingleton(new PlanningViewModel());
        RegisterSingleton(new VersionsViewModel());
        RegisterSingleton(new TaskManagerViewModel(GetRequiredService<ITaskService>()));
        RegisterSingleton(new TaskDetailViewModel());
        RegisterSingleton(new StatisticsViewModel(GetRequiredService<IStatisticsService>()));
        RegisterSingleton(new SettingsViewModel());
        RegisterSingleton(new FocusModeViewModel(GetRequiredService<IFocusModeService>()));
        RegisterSingleton(new OverlayViewModel());
        RegisterSingleton(new CreateProjectViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IDialogService>()));
        RegisterSingleton(new StartViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IRecentFilesService>()));

        RegisterSingleton(new MainViewModel(
            GetRequiredService<IProjectService>(),
            GetRequiredService<IExternalResourceService>(),
            GetRequiredService<IFileSystemProjectExplorerService>(),
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
