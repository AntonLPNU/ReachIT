// Manages the recent external files panel below project explorer.
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Models;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class RecentExternalFilesPanelViewModel : ViewModelBase
{
    private readonly IRecentFilesService _recentFilesService;
    private readonly IExternalResourceService _externalResourceService;
    private readonly IProjectService _projectService;
    private RecentExternalFileItem? _selectedItem;
    private readonly AsyncCommand _attachToProjectCommand;
    private readonly AsyncCommand _copyIntoProjectCommand;
    private readonly AsyncCommand _saveAsLinkCommand;
    private readonly AsyncCommand _removeFromRecentCommand;

    public RecentExternalFilesPanelViewModel(
        IRecentFilesService recentFilesService,
        IExternalResourceService externalResourceService,
        IProjectService projectService)
    {
        _recentFilesService = recentFilesService;
        _externalResourceService = externalResourceService;
        _projectService = projectService;

        _attachToProjectCommand = new AsyncCommand(_ => AttachToProjectAsync(), _ => SelectedItem is not null);
        _copyIntoProjectCommand = new AsyncCommand(_ => CopyIntoProjectAsync(), _ => SelectedItem is not null);
        _saveAsLinkCommand = new AsyncCommand(_ => SaveAsLinkAsync(), _ => SelectedItem is not null);
        _removeFromRecentCommand = new AsyncCommand(_ => RemoveFromRecentAsync(), _ => SelectedItem is not null);

        AttachToProjectCommand = _attachToProjectCommand;
        CopyIntoProjectCommand = _copyIntoProjectCommand;
        SaveAsLinkCommand = _saveAsLinkCommand;
        RemoveFromRecentCommand = _removeFromRecentCommand;
    }

    public ObservableCollection<RecentExternalFileItem> Items { get; } = new();

    public RecentExternalFileItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                _attachToProjectCommand.RaiseCanExecuteChanged();
                _copyIntoProjectCommand.RaiseCanExecuteChanged();
                _saveAsLinkCommand.RaiseCanExecuteChanged();
                _removeFromRecentCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand AttachToProjectCommand { get; }
    public ICommand CopyIntoProjectCommand { get; }
    public ICommand SaveAsLinkCommand { get; }
    public ICommand RemoveFromRecentCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Items.Clear();
        var items = await _recentFilesService.GetRecentExternalFilesAsync(cancellationToken).ConfigureAwait(true);
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    private async Task AttachToProjectAsync()
    {
        if (SelectedItem is null || _projectService.CurrentProject is null)
        {
            return;
        }

        await _externalResourceService.AttachAsync(_projectService.CurrentProject.Id, SelectedItem.SourcePathOrUrl).ConfigureAwait(true);
    }

    private async Task CopyIntoProjectAsync()
    {
        if (SelectedItem is null || _projectService.CurrentProject is null)
        {
            return;
        }

        await _externalResourceService.CopyIntoProjectAsync(_projectService.CurrentProject.Id, SelectedItem.SourcePathOrUrl).ConfigureAwait(true);
    }

    private async Task SaveAsLinkAsync()
    {
        if (SelectedItem is null || _projectService.CurrentProject is null)
        {
            return;
        }

        try
        {
            await _externalResourceService.SaveAsLinkAsync(_projectService.CurrentProject.Id, SelectedItem.SourcePathOrUrl).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Blocked unsafe link.\n\n{ex.Message}", "ReachIT Security", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task RemoveFromRecentAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        await _recentFilesService.RemoveRecentExternalFileAsync(SelectedItem.Id).ConfigureAwait(true);
        await LoadAsync().ConfigureAwait(true);
    }
}
