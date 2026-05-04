// Executes Git commands without shell interpolation.
using System.Diagnostics;
using ReachIT.Application.Contracts;

namespace ReachIT.Application.Services;

public sealed class GitService : IGitService
{
    public async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory is required.", nameof(workingDirectory));
        }

        if (arguments.Count == 0)
        {
            throw new ArgumentException("At least one Git argument is required.", nameof(arguments));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git. Make sure Git is installed and available in PATH.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var output = await outputTask.ConfigureAwait(false);
        var error = await errorTask.ConfigureAwait(false);

        return new GitCommandResult(process.ExitCode, output, error);
    }
}
