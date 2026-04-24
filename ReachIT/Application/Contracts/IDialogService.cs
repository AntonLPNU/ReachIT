// Defines safe dialog interactions for file-based actions.
namespace ReachIT.Application.Contracts;

public interface IDialogService
{
    string? ShowOpenFileDialog(string filter);
    string? ShowSaveFileDialog(string filter);
    string? ShowOpenFolderDialog(string? initialDirectory = null);
}
