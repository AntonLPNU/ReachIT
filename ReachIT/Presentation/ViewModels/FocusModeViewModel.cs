// Represents focus mode controls and status for the UI.
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using ReachIT.Application.Contracts;
using ReachIT.Domain.Enums;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FocusModeViewModel : ViewModelBase, IDisposable
{
    private readonly IFocusModeService _focusModeService;
    private readonly IOverlayService _overlayService;
    private FocusModeType _selectedMode = FocusModeType.Light;
    private bool _isActive;
    private string _sessionDurationText = "00:00:00";
    private readonly DispatcherTimer _timer;

    public FocusModeViewModel(IFocusModeService focusModeService, IOverlayService overlayService)
    {
        _focusModeService = focusModeService;
        _overlayService = overlayService;

        AllowedApplications = new ObservableCollection<string>(["ReachIT", "devenv", "Code"]);
        BlockedApplications = new ObservableCollection<string>(["chrome", "msedge", "discord", "spotify"]);
        DistractionLog = new ObservableCollection<string>();

        StartCommand = new AsyncCommand(_ => StartAsync(), _ => !_focusModeService.IsActive);
        PauseCommand = new AsyncCommand(_ => PauseAsync(), _ => _focusModeService.IsActive);
        StopCommand = new AsyncCommand(_ => StopAsync(), _ => _focusModeService.IsActive || _sessionDurationText != "00:00:00");

        _focusModeService.StateChanged += OnFocusStateChanged;
        _focusModeService.DistractionDetected += OnDistractionDetected;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateTimerText();
    }

    public ObservableCollection<string> AllowedApplications { get; }
    public ObservableCollection<string> BlockedApplications { get; }
    public ObservableCollection<string> DistractionLog { get; }

    public FocusModeType SelectedMode
    {
        get => _selectedMode;
        set => SetProperty(ref _selectedMode, value);
    }

    public bool IsActive
    {
        get => _isActive;
        private set 
        {
            if (SetProperty(ref _isActive, value))
            {
                if (_isActive) _timer.Start();
                else _timer.Stop();

                ((AsyncCommand)StartCommand).RaiseCanExecuteChanged();
                ((AsyncCommand)PauseCommand).RaiseCanExecuteChanged();
                ((AsyncCommand)StopCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public string SessionDurationText
    {
        get => _sessionDurationText;
        private set => SetProperty(ref _sessionDurationText, value);
    }

    public ICommand StartCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }

    private async Task StartAsync()
    {
        await _focusModeService.StartAsync(SelectedMode).ConfigureAwait(true);
    }

    private async Task PauseAsync()
    {
        await _focusModeService.PauseAsync().ConfigureAwait(true);
    }

    private async Task StopAsync()
    {
        await _focusModeService.StopAsync().ConfigureAwait(true);
        SessionDurationText = "00:00:00";
        DistractionLog.Clear();
    }

    private void OnFocusStateChanged()
    {
        // Must marshal to UI thread implicitly using standard async command flow or directly set property from UI Context
        App.Current.Dispatcher.Invoke(() =>
        {
            IsActive = _focusModeService.IsActive;
            UpdateTimerText();
        });
    }

    private void OnDistractionDetected(string appName)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            var msg = $"{DateTime.Now:HH:mm:ss} - Detected distraction: {appName}";
            if (!DistractionLog.Contains(msg))
            {
                DistractionLog.Add(msg);
            }

            _overlayService.ShowMessage($"Focus Warning: {appName} might be a distraction.");
        });
    }

    private void UpdateTimerText()
    {
        var duration = _focusModeService.SessionDuration;
        SessionDurationText = duration.ToString(@"hh\:mm\:ss");
    }

    public void Dispose()
    {
        _focusModeService.StateChanged -= OnFocusStateChanged;
        _focusModeService.DistractionDetected -= OnDistractionDetected;
        _timer.Stop();
    }
}
