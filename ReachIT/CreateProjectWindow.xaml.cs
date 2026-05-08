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
        Closed += OnClosed;
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
        SetDialogResult(true);
        Close();
    }

    private void OnRequestCancel(object? sender, EventArgs e)
    {
        SetDialogResult(false);
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        SetDialogResult(false);
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is CreateProjectViewModel viewModel)
        {
            viewModel.RequestCreated -= OnRequestCreated;
            viewModel.RequestCancel -= OnRequestCancel;
        }
    }

    private void SetDialogResult(bool result)
    {
        try
        {
            DialogResult = result;
        }
        catch (InvalidOperationException)
        {
            // Window can be hosted or already closing; closing without a dialog result is still a safe cancel path.
        }
    }
}
