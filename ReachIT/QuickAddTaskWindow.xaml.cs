using System.Windows;
using System.Windows.Input;

namespace ReachIT;

public partial class QuickAddTaskWindow : Window
{
    public QuickAddTaskWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        TitleBox.Focus();
        TitleBox.SelectAll();
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
