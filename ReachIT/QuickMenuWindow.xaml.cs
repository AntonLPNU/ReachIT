using System.Windows;
using System.Windows.Input;

namespace ReachIT;

public partial class QuickMenuWindow : Window
{
    public QuickMenuWindow()
    {
        InitializeComponent();
    }

    private void TitleArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // WPF can throw if the mouse capture is interrupted while dragging.
        }
    }
}
