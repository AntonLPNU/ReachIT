// Holds overlay state for temporary shell notifications.
namespace ReachIT.Presentation.ViewModels;

public sealed class OverlayViewModel : ViewModelBase
{
    private string? _message;
    private bool _isVisible;

    public string? Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }
}
