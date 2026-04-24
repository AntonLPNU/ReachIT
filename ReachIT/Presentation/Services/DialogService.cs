// Wraps basic file dialogs for safe user interactions.
using Microsoft.Win32;
using ReachIT.Application.Contracts;

namespace ReachIT.Presentation.Services;

public sealed class DialogService : IDialogService
{
    public string? ShowOpenFileDialog(string filter)
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            CheckFileExists = true,
            Multiselect = false
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter)
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenFolderDialog(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog();
        if (!string.IsNullOrWhiteSpace(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
