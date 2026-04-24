// Defines overlay messaging behavior in the shell.
namespace ReachIT.Application.Contracts;

public interface IOverlayService
{
    string? CurrentMessage { get; }
    void ShowMessage(string message);
    void Hide();
}
