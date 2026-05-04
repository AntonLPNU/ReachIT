// Hosts the start menu flow before opening workspace.
using System.Windows;
using System.Windows.Input;
using ReachIT.Presentation.ViewModels;

namespace ReachIT;

public partial class StartWindow : Window
{
    public StartWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is StartViewModel viewModel)
        {
            await viewModel.LoadAsync().ConfigureAwait(true);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is StartViewModel oldViewModel)
        {
            oldViewModel.RequestClose -= OnRequestClose;
        }

        if (e.NewValue is StartViewModel newViewModel)
        {
            newViewModel.RequestClose += OnRequestClose;
        }
    }

    private void OnRequestClose(object? sender, bool dialogResult)
    {
        DialogResult = dialogResult;
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object sender, RoutedEventArgs e)
    {
        ToggleWindowState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
