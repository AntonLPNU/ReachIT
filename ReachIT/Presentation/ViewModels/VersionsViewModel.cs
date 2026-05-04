// Placeholder version control workspace view model.
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public class SnapshotInfo
{
    public string GroupName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class VersionsViewModel : ViewModelBase
{
    private readonly IDatabaseService _databaseService;

    public VersionsViewModel(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
        CreateSnapshotCommand = new RelayCommand(_ => CreateSnapshot());
        RestoreSnapshotCommand = new RelayCommand(RestoreSnapshot, _ => SelectedSnapshot != null);
    }

    public ObservableCollection<SnapshotInfo> Snapshots { get; } = new();

    private SnapshotInfo? _selectedSnapshot;
    public SnapshotInfo? SelectedSnapshot
    {
        get => _selectedSnapshot;
        set
        {
            if (SetProperty(ref _selectedSnapshot, value))
            {
                ((RelayCommand)RestoreSnapshotCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand CreateSnapshotCommand { get; }
    public ICommand RestoreSnapshotCommand { get; }

    public Task LoadAsync(CancellationToken cancellationToken = default)
    {
        RefreshSnapshots();
        return Task.CompletedTask;
    }

    private void RefreshSnapshots()
    {
        Snapshots.Clear();
        var currentDbPath = _databaseService.DatabasePath;
        var backupDir = Path.Combine(Path.GetDirectoryName(currentDbPath) ?? string.Empty, "Snapshots");

        if (Directory.Exists(backupDir))
        {
            var files = Directory.GetFiles(backupDir, "*.db").OrderByDescending(File.GetCreationTime);
            foreach (var f in files)
            {
                Snapshots.Add(new SnapshotInfo 
                { 
                    Path = f, 
                    GroupName = Path.GetFileNameWithoutExtension(f), 
                    CreatedAt = File.GetCreationTime(f) 
                });
            }
        }
    }

    private void CreateSnapshot()
    {
        try
        {
            var currentDbPath = _databaseService.DatabasePath;
            var backupDir = Path.Combine(Path.GetDirectoryName(currentDbPath) ?? string.Empty, "Snapshots");

            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            var snapshotName = $"Snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            var destPath = Path.Combine(backupDir, snapshotName);

            File.Copy(currentDbPath, destPath, overwrite: true);
            RefreshSnapshots();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create snapshot: {ex.Message}");
        }
    }

    private void RestoreSnapshot(object? _ = null)
    {
        if (SelectedSnapshot == null) return;

        var result = MessageBox.Show($"Are you sure you want to restore from relative snapshot '{SelectedSnapshot.GroupName}'? Current data will be overwritten and application might need a restart.", "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var currentDbPath = _databaseService.DatabasePath;
                File.Copy(SelectedSnapshot.Path, currentDbPath, overwrite: true);
                MessageBox.Show("Snapshot restored successfully. Please restart the application for changes to take effect.");
            }
            catch (Exception ex)
            {
                 MessageBox.Show($"Failed to restore snapshot: {ex.Message}");
            }
        }
    }
}
