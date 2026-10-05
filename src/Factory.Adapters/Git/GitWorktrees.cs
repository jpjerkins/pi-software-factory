using Factory.Adapters.Shell;
using Factory.Application.Ports;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.Git;

/// <summary>
/// Gives each issue its own branch and worktree under the repo root:
/// <c>&lt;root&gt;/main</c> is the factory's clone, <c>&lt;root&gt;/&lt;slug&gt;</c> a worktree per issue.
/// Never reuses, forces or removes anything it did not just create.
/// </summary>
public sealed class GitWorktrees(string repoRoot, string remoteUrl) : IWorktrees
{
    private const string MainFolder = "main";
    private readonly ProcessRunner _process = new();

    private string MainClone => Path.Combine(repoRoot, MainFolder);

    public async Task<Worktree> PrepareAsync(Issue issue, CancellationToken ct)
    {
        var branch = WorkBranch.For(issue);
        var path = Path.Combine(repoRoot, branch.Slug);

        await EnsureCloneAsync(ct);
        await GitAsync(["-C", MainClone, "fetch", "origin"], ct);

        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new WorktreeAlreadyExistsException($"Worktree folder '{path}' already exists; another session may hold it.");
        }

        if (await BranchExistsAsync(branch.BranchName, ct))
        {
            throw new WorktreeAlreadyExistsException($"Branch '{branch.BranchName}' already exists; another session may hold it.");
        }

        await GitAsync(["-C", MainClone, "worktree", "add", path, "-b", branch.BranchName, "origin/main"], ct);
        return new Worktree(path, branch.BranchName);
    }

    public async Task RemoveAsync(Worktree worktree, CancellationToken ct)
    {
        await GitAsync(["-C", MainClone, "worktree", "remove", worktree.Path], ct);
        await GitAsync(["-C", MainClone, "branch", "-d", worktree.Branch], ct);
    }

    private async Task EnsureCloneAsync(CancellationToken ct)
    {
        if (!Directory.Exists(MainClone))
        {
            Directory.CreateDirectory(repoRoot);
            await GitAsync(["clone", remoteUrl, MainClone], ct);
        }
    }

    private async Task<bool> BranchExistsAsync(string branch, CancellationToken ct)
    {
        var result = await _process.RunAsync(
            "git", ["-C", MainClone, "show-ref", "--verify", "--quiet", $"refs/heads/{branch}"], null, ct);
        return result.ExitCode == 0;
    }

    private async Task GitAsync(string[] args, CancellationToken ct)
    {
        var result = await _process.RunAsync("git", args, null, ct);
        if (result.ExitCode != 0)
        {
            throw new GitCommandException(string.Join(' ', args), result.ExitCode, result.StdErr);
        }
    }
}
