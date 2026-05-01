// Defines in-window navigation between workspace views.
namespace ReachIT.Application.Contracts;

public interface INavigationService
{
    object? CurrentViewModel { get; }
    event EventHandler<object?>? Navigated;
    event EventHandler? NavigationStateChanged;

    bool CanGoBack { get; }
    bool CanGoForward { get; }

    void NavigateTo(object viewModel);
    bool GoBack();
    bool GoForward();
}
