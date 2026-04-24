// Provides a minimal shell overlay message service.
using ReachIT.Application.Contracts;

namespace ReachIT.Presentation.Services;

public sealed class OverlayService : IOverlayService
{
    public string? CurrentMessage { get; private set; }

    public void ShowMessage(string message)
    {
        CurrentMessage = message;
    }

    public void Hide()
    {
        CurrentMessage = null;
    }
}
