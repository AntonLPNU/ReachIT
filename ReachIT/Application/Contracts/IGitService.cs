// Provides small Git command helpers scoped to a project directory.
namespace ReachIT.Application.Contracts;

public interface IGitService
{
    Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

public sealed record GitCommandResult(int ExitCode, string Output, string Error)
{
    public string CombinedOutput
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Output))
            {
                return Error;
            }

            if (string.IsNullOrWhiteSpace(Error))
            {
                return Output;
            }

            return $"{Output.TrimEnd()}{Environment.NewLine}{Error}";
        }
    }
}
