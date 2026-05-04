using System.Windows.Input;
using ReachIT.Presentation.Commands;

namespace ReachIT.Presentation.ViewModels;

public sealed class FloatingLogoViewModel : ViewModelBase
{
    public event EventHandler? ToggleMenuRequested;
    public event EventHandler? HideRequested;

    public FloatingLogoViewModel()
    {
        ToggleMenuCommand = new RelayCommand(_ => ToggleMenuRequested?.Invoke(this, EventArgs.Empty));
        HideCommand = new RelayCommand(_ => HideRequested?.Invoke(this, EventArgs.Empty));
    }

    public ICommand ToggleMenuCommand { get; }
    public ICommand HideCommand { get; }
}
