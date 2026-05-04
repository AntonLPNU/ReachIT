using System.Windows.Input;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class QuickMenuViewModel : ViewModelBase
{
    public event EventHandler? NewTaskRequested;
    public event EventHandler? ProjectExplorerRequested;
    public event EventHandler? FocusModeRequested;
    public event EventHandler? StatisticsRequested;
    public event EventHandler? MainWindowRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? HideRequested;
    public event EventHandler? ExitRequested;

    public QuickMenuViewModel()
    {
        NewTaskCommand = new RelayCommand(_ => NewTaskRequested?.Invoke(this, EventArgs.Empty));
        ProjectExplorerCommand = new RelayCommand(_ => ProjectExplorerRequested?.Invoke(this, EventArgs.Empty));
        FocusModeCommand = new RelayCommand(_ => FocusModeRequested?.Invoke(this, EventArgs.Empty));
        StatisticsCommand = new RelayCommand(_ => StatisticsRequested?.Invoke(this, EventArgs.Empty));
        MainWindowCommand = new RelayCommand(_ => MainWindowRequested?.Invoke(this, EventArgs.Empty));
        SettingsCommand = new RelayCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty));
        HideCommand = new RelayCommand(_ => HideRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(_ => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public ICommand NewTaskCommand { get; }
    public ICommand ProjectExplorerCommand { get; }
    public ICommand FocusModeCommand { get; }
    public ICommand StatisticsCommand { get; }
    public ICommand MainWindowCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand ExitCommand { get; }
}
