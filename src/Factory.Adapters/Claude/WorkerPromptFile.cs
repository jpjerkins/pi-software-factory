using System.Text.RegularExpressions;
using Factory.Domain.Issues;

namespace Factory.Adapters.Claude;

/// <summary>Renders the worker prompt template into the run directory.</summary>
public sealed partial class WorkerPromptFile(string templatePath)
{
    public const string FileName = "prompt.md";

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();

    /// <param name="title">The issue title, shown to the worker (workers cannot run <c>gh</c>).</param>
    /// <param name="body">The issue body, written verbatim; blank becomes "(no description)".</param>
    /// <returns>The path of the prompt written inside <paramref name="runDir"/>.</returns>
    public async Task<string> WriteAsync(string runDir, IssueNumber issue, string title, string body, string worktreePath, CancellationToken ct)
    {
        var template = await File.ReadAllTextAsync(templatePath, ct);
        var values = new Dictionary<string, string>
        {
            ["issue"] = issue.Value.ToString(),
            ["title"] = title,
            ["body"] = string.IsNullOrWhiteSpace(body) ? "(no description)" : body,
            ["worktree"] = worktreePath,
            ["run_dir"] = runDir,
        };
        // One pass, so placeholder-like text inside a substituted value is never substituted again.
        var text = Placeholder().Replace(template, m => values.GetValueOrDefault(m.Groups[1].Value, m.Value));

        var path = Path.Combine(runDir, FileName);
        await File.WriteAllTextAsync(path, text, ct);
        return path;
    }
}
