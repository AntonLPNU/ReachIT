// Defines in-window navigation between workspace views.
namespace ReachIT.Application.Contracts;

public interface INavigationService
{
    object? CurrentViewModel { get; }
    event EventHandler<object?>? Navigated;
    void NavigateTo(object viewModel);
}
