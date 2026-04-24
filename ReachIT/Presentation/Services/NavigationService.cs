// Handles in-window view model navigation.
using ReachIT.Application.Contracts;

namespace ReachIT.Presentation.Services;

public sealed class NavigationService : INavigationService
{
    public object? CurrentViewModel { get; private set; }

    public event EventHandler<object?>? Navigated;

    public void NavigateTo(object viewModel)
    {
        CurrentViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        Navigated?.Invoke(this, CurrentViewModel);
    }
}
