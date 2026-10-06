using Factory.Adapters.Claude;
using Factory.Adapters.Tests.Storage;
using Factory.Domain.Issues;

namespace Factory.Adapters.Tests.Claude;

public sealed class WorkerPromptFileTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempRunsRoot _root = new();

    public void Dispose() => _root.Dispose();

    private static string RealTemplate => Path.Combine(AppContext.BaseDirectory, "assets", "worker", "prompt.md.template");

    private async Task<string> RenderAsync(string template)
    {
        var templatePath = Path.Combine(_root.Path, "t.template");
        File.WriteAllText(templatePath, template);
        var path = await new WorkerPromptFile(templatePath).WriteAsync(_root.Path, new IssueNumber(103), "/wt/path", Ct);
        Assert.Equal(Path.Combine(_root.Path, "prompt.md"), path);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task Fills_in_issue_worktree_and_run_dir()
    {
        var text = await RenderAsync("#{{issue}} in {{worktree}} run {{run_dir}} again #{{issue}}");

        Assert.Equal($"#103 in /wt/path run {_root.Path} again #103", text);
    }

    [Fact]
    public async Task The_shipped_template_leaves_no_placeholders_and_names_the_contract()
    {
        var path = await new WorkerPromptFile(RealTemplate).WriteAsync(_root.Path, new IssueNumber(103), "/wt/path", Ct);
        var text = File.ReadAllText(path);

        Assert.DoesNotContain("{{", text);
        Assert.Contains("task-guide #103", text);
        Assert.Contains("/wt/path", text);
        Assert.Contains(Path.Combine(_root.Path, "result.json"), text);
        Assert.Contains("plan_ready", text);
        foreach (var field in new[] { "status", "summary", "tests_run", "files_touched", "questions", "problems_found" })
        {
            Assert.Contains(field, text);
        }
    }
}
