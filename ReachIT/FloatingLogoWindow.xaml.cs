using System.Windows;
using System.Windows.Input;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class FloatingLogoWindow : Window
{
    public event EventHandler? PositionChangedByUser;

    public FloatingLogoWindow()
    {
        InitializeComponent();
    }

    private void Logo_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            OpenActionMenu();
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
            KeepInsideVirtualScreen();
            PositionChangedByUser?.Invoke(this, EventArgs.Empty);
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if Windows loses the pressed-button state mid-message.
        }
    }

    private void Logo_MouseMove(object sender, MouseEventArgs e)
    {
    }

    private void Logo_MouseEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is FloatingLogoViewModel viewModel && viewModel.ShowMenuCommand.CanExecute(null))
        {
            viewModel.ShowMenuCommand.Execute(null);
        }
    }

    private void Logo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
    }

    private void Logo_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is FloatingLogoViewModel viewModel && viewModel.HideCommand.CanExecute(null))
        {
            viewModel.HideCommand.Execute(null);
        }
    }

    private void KeepInsideVirtualScreen()
    {
        var minLeft = SystemParameters.VirtualScreenLeft;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxLeft = minLeft + SystemParameters.VirtualScreenWidth - Width;
        var maxTop = minTop + SystemParameters.VirtualScreenHeight - Height;

        Left = Math.Min(Math.Max(Left, minLeft), maxLeft);
        Top = Math.Min(Math.Max(Top, minTop), maxTop);
    }

    private void OpenActionMenu()
    {
        if (DataContext is FloatingLogoViewModel viewModel && viewModel.ToggleMenuCommand.CanExecute(null))
        {
            viewModel.ToggleMenuCommand.Execute(null);
        }
    }
}
