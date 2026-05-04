// Hosts create-project form flow.
using System.Windows;
using System.Windows.Input;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class CreateProjectWindow : Window
{
    public CreateProjectWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is CreateProjectViewModel oldViewModel)
        {
            oldViewModel.RequestCreated -= OnRequestCreated;
            oldViewModel.RequestCancel -= OnRequestCancel;
        }

        if (e.NewValue is CreateProjectViewModel newViewModel)
        {
            newViewModel.RequestCreated += OnRequestCreated;
            newViewModel.RequestCancel += OnRequestCancel;
        }
    }

    private void OnRequestCreated(object? sender, string ritPath)
    {
        DialogResult = true;
        Close();
    }

    private void OnRequestCancel(object? sender, EventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
