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
        var path = await new WorkerPromptFile(templatePath).WriteAsync(_root.Path, new IssueNumber(103), "Fix it", "Some body", "/wt/path", Ct);
        Assert.Equal(Path.Combine(_root.Path, "prompt.md"), path);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task Fills_in_issue_worktree_and_run_dir()
    {
        var text = await RenderAsync("#{{issue}} in {{worktree}} run {{run_dir}} again #{{issue}}");

        Assert.Equal($"#103 in /wt/path run {_root.Path} again #103", text);
    }

    private async Task<string> RenderAsync(string template, string title, string body)
    {
        var templatePath = Path.Combine(_root.Path, "t.template");
        File.WriteAllText(templatePath, template);
        var path = await new WorkerPromptFile(templatePath).WriteAsync(_root.Path, new IssueNumber(103), title, body, "/wt/path", Ct);
        return File.ReadAllText(path);
    }

    [Fact]
    public async Task Writes_the_issue_title_and_body()
    {
        var text = await RenderAsync("**#{{issue}}: {{title}}**\n\n{{body}}\n", "Fix the bug", "Line one\nLine two");

        Assert.Equal("**#103: Fix the bug**\n\nLine one\nLine two\n", text);
    }

    [Fact]
    public async Task An_empty_body_reads_no_description()
    {
        var text = await RenderAsync("{{body}}", "T", "  \n");

        Assert.Equal("(no description)", text);
    }

    [Fact]
    public async Task Placeholder_like_text_in_the_title_or_body_stays_verbatim()
    {
        var text = await RenderAsync("{{title}}|{{body}}|{{worktree}}", "use {{worktree}}", "a {b} {{run_dir}} {{issue}} {0}");

        Assert.Equal("use {{worktree}}|a {b} {{run_dir}} {{issue}} {0}|/wt/path", text);
    }

    [Fact]
    public async Task The_shipped_template_shows_the_issue_and_says_gh_is_unavailable()
    {
        var path = await new WorkerPromptFile(RealTemplate).WriteAsync(_root.Path, new IssueNumber(103), "Fix the bug", "Details here", "/wt/path", Ct);
        var text = File.ReadAllText(path);

        Assert.Contains("## The issue", text);
        Assert.Contains("**#103: Fix the bug**", text);
        Assert.Contains("Details here", text);
        Assert.Contains("Read the issue above", text);
        Assert.Contains("Workers cannot use `gh`", text);
    }

    [Fact]
    public async Task The_shipped_template_leaves_no_placeholders_and_names_the_contract()
    {
        var path = await new WorkerPromptFile(RealTemplate).WriteAsync(_root.Path, new IssueNumber(103), "Fix it", "Some body", "/wt/path", Ct);
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
