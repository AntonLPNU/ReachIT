// Represents focus mode controls and status for the UI.
using System.Collections.ObjectModel;
using System.Windows.Input;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FocusModeViewModel : ViewModelBase
{
    private readonly IFocusModeService _focusModeService;
    private FocusModeType _selectedMode = FocusModeType.Light;
    private bool _isActive;

    public FocusModeViewModel(IFocusModeService focusModeService)
    {
        _focusModeService = focusModeService;

        AllowedApplications = new ObservableCollection<string>(["ReachIT"]);
        StartCommand = new AsyncCommand(_ => StartAsync());
        StopCommand = new AsyncCommand(_ => StopAsync());
    }

    public ObservableCollection<string> AllowedApplications { get; }

    public FocusModeType SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public bool IsActive
    {
        get => _isActive;
        private set => SetProperty(ref _isActive, value);
    }

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }

    private async Task StartAsync()
    {
        await _focusModeService.StartAsync(SelectedMode).ConfigureAwait(true);
        IsActive = _focusModeService.IsActive;
    }

    private async Task StopAsync()
    {
        await _focusModeService.StopAsync().ConfigureAwait(true);
        IsActive = _focusModeService.IsActive;
    }
}
