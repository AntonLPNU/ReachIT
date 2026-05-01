// Handles in-window view model navigation.
using ReachIT.Application.Contracts;

namespace ReachIT.Presentation.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Stack<object> _backStack = new();
    private readonly Stack<object> _forwardStack = new();

    public object? CurrentViewModel { get; private set; }
    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    public event EventHandler<object?>? Navigated;
    public event EventHandler? NavigationStateChanged;

    public void NavigateTo(object viewModel)
    {
        var target = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        if (ReferenceEquals(CurrentViewModel, target))
        {
            return;
        }

        if (CurrentViewModel is not null)
        {
            _backStack.Push(CurrentViewModel);
            _forwardStack.Clear();
        }

        CurrentViewModel = target;
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        Navigated?.Invoke(this, CurrentViewModel);
    }

    public bool GoBack()
    {
        if (!CanGoBack)
        {
            return false;
        }

        if (CurrentViewModel is not null)
        {
            _forwardStack.Push(CurrentViewModel);
        }

        CurrentViewModel = _backStack.Pop();
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        Navigated?.Invoke(this, CurrentViewModel);
        return true;
    }

    public bool GoForward()
    {
        if (!CanGoForward)
        {
            return false;
        }

        if (CurrentViewModel is not null)
        {
            _backStack.Push(CurrentViewModel);
        }

        CurrentViewModel = _forwardStack.Pop();
        NavigationStateChanged?.Invoke(this, EventArgs.Empty);
        Navigated?.Invoke(this, CurrentViewModel);
        return true;
    }
}
