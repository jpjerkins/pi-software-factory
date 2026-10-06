using Factory.Domain.Issues;

namespace Factory.Adapters.Claude;

/// <summary>Renders the worker prompt template into the run directory.</summary>
public sealed class WorkerPromptFile(string templatePath)
{
    public const string FileName = "prompt.md";

    /// <returns>The path of the prompt written inside <paramref name="runDir"/>.</returns>
    public async Task<string> WriteAsync(string runDir, IssueNumber issue, string worktreePath, CancellationToken ct)
    {
        var template = await File.ReadAllTextAsync(templatePath, ct);
        var text = template
            .Replace("{{issue}}", issue.Value.ToString())
            .Replace("{{worktree}}", worktreePath)
            .Replace("{{run_dir}}", runDir);

        var path = Path.Combine(runDir, FileName);
        await File.WriteAllTextAsync(path, text, ct);
        return path;
    }
}
