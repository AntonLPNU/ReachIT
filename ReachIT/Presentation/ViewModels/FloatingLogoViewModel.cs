using System.Windows.Input;
using System.Windows.Threading;
using ReachIT.Application.Contracts;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FloatingLogoViewModel : ViewModelBase, IDisposable
{
    private readonly IFocusModeService _focusModeService;
    private readonly DispatcherTimer _timer;
    private bool _isFocusActive;
    private string _focusDurationText = "00:00";
    private string _statusText = "ReachIT";

    public event EventHandler? ToggleMenuRequested;
    public event EventHandler? ShowMenuRequested;
    public event EventHandler? HideRequested;

    public FloatingLogoViewModel(IFocusModeService focusModeService)
    {
        _focusModeService = focusModeService;
        ToggleMenuCommand = new RelayCommand(_ => ToggleMenuRequested?.Invoke(this, EventArgs.Empty));
        ShowMenuCommand = new RelayCommand(_ => ShowMenuRequested?.Invoke(this, EventArgs.Empty));
        HideCommand = new RelayCommand(_ => HideRequested?.Invoke(this, EventArgs.Empty));

        _focusModeService.StateChanged += OnFocusStateChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateFocusState();
        UpdateFocusState();
    }

    public ICommand ToggleMenuCommand { get; }
    public ICommand ShowMenuCommand { get; }
    public ICommand HideCommand { get; }

    public bool IsFocusActive
    {
        get => _isFocusActive;
        private set => SetProperty(ref _isFocusActive, value);
    }

    public string FocusDurationText
    {
        get => _focusDurationText;
        private set => SetProperty(ref _focusDurationText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private void OnFocusStateChanged()
    {
        App.Current.Dispatcher.Invoke(UpdateFocusState);
    }

    private void UpdateFocusState()
    {
        IsFocusActive = _focusModeService.IsActive;
        if (IsFocusActive)
        {
            _timer.Start();
            var duration = _focusModeService.SessionDuration;
            FocusDurationText = duration.TotalHours >= 1
                ? duration.ToString(@"hh\:mm")
                : duration.ToString(@"mm\:ss");
            StatusText = $"Focus active - {FocusDurationText}";
            return;
        }

        _timer.Stop();
        FocusDurationText = "00:00";
        StatusText = "ReachIT";
    }

    public void Dispose()
    {
        _focusModeService.StateChanged -= OnFocusStateChanged;
        _timer.Stop();
    }
}
