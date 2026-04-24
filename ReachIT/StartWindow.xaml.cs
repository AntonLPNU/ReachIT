// Hosts the start menu flow before opening workspace.
using System.Windows;
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
}
