// Holds statistics data for the statistics workspace.
using System.Collections.ObjectModel;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;

namespace ReachIT.Presentation.ViewModels;

public sealed class StatisticsViewModel : ViewModelBase
{
    private readonly IStatisticsService _statisticsService;
    private ProjectStats _projectStats = new();

    public StatisticsViewModel(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public ProjectStats ProjectStats
    {
        get => _projectStats;
        private set => SetProperty(ref _projectStats, value);
    }

    public ObservableCollection<ProductivityStat> Productivity { get; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        ProjectStats = await _statisticsService.GetProjectStatsAsync(cancellationToken).ConfigureAwait(true);
        var list = await _statisticsService.GetProductivityStatsAsync(cancellationToken).ConfigureAwait(true);

        Productivity.Clear();
        foreach (var item in list)
        {
            Productivity.Add(item);
        }
    }
}
