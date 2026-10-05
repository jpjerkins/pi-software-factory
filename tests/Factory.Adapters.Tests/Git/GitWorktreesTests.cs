using Factory.Adapters.Git;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.Tests.Git;

public class GitWorktreesTests
{
    private static readonly Issue Issue103 = new(
        new IssueNumber(103), "GitHub sync", true, DateTimeOffset.UnixEpoch,
        ["build", "agent:claude", "lane:adapters"], [], [], []);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GitWorktrees Create(TempGitRemote remote) => new(remote.RepoRoot, remote.RemoteUrl);

    [Fact]
    public async Task Clones_the_remote_into_main_when_it_is_missing()
    {
        using var remote = new TempGitRemote();

        await Create(remote).PrepareAsync(Issue103, Ct);

        Assert.True(File.Exists(Path.Combine(remote.Main, "README.md")));
    }

    [Fact]
    public async Task Creates_the_lane_branch_and_a_slug_named_worktree_from_origin_main()
    {
        using var remote = new TempGitRemote();

        var worktree = await Create(remote).PrepareAsync(Issue103, Ct);

        Assert.Equal(new Worktree(Path.Combine(remote.RepoRoot, "103-github-sync"), "adapters/103-github-sync"), worktree);
        Assert.True(File.Exists(Path.Combine(worktree.Path, "README.md")));
        Assert.Equal("adapters/103-github-sync", TempGitRemote.Run(worktree.Path, "branch", "--show-current").Trim());
    }

    [Fact]
    public async Task Branches_from_the_latest_origin_main()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        await git.PrepareAsync(Issue103, Ct);
        var other = Path.Combine(remote.RepoRoot, "other");
        TempGitRemote.Run(remote.RepoRoot, "clone", remote.RemoteUrl, other);
        TempGitRemote.Commit(other, "newer.txt");
        TempGitRemote.Run(other, "push", "origin", "HEAD:main");

        var next = Issue103 with { Number = new IssueNumber(104), Title = "Next thing" };
        var worktree = await git.PrepareAsync(next, Ct);

        Assert.True(File.Exists(Path.Combine(worktree.Path, "newer.txt")));
    }

    [Fact]
    public async Task Throws_when_the_worktree_folder_already_exists()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        Directory.CreateDirectory(Path.Combine(remote.RepoRoot, "103-github-sync"));

        await Assert.ThrowsAsync<WorktreeAlreadyExistsException>(() => git.PrepareAsync(Issue103, Ct));
    }

    [Fact]
    public async Task Throws_when_the_branch_already_exists_and_leaves_it_alone()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        var first = await git.PrepareAsync(Issue103, Ct);
        TempGitRemote.Run(first.Path, "worktree", "remove", first.Path);
        Assert.False(Directory.Exists(first.Path));

        await Assert.ThrowsAsync<WorktreeAlreadyExistsException>(() => git.PrepareAsync(Issue103, Ct));
        Assert.Contains("adapters/103-github-sync", TempGitRemote.Run(remote.Main, "branch", "--list"));
    }

    [Fact]
    public async Task Removes_a_clean_merged_worktree_and_its_branch()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        var worktree = await git.PrepareAsync(Issue103, Ct);

        await git.RemoveAsync(worktree, Ct);

        Assert.False(Directory.Exists(worktree.Path));
        Assert.DoesNotContain("adapters/103-github-sync", TempGitRemote.Run(remote.Main, "branch", "--list"));
    }

    [Fact]
    public async Task Refuses_to_remove_a_dirty_worktree()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        var worktree = await git.PrepareAsync(Issue103, Ct);
        File.WriteAllText(Path.Combine(worktree.Path, "uncommitted.txt"), "work in progress");

        await Assert.ThrowsAsync<GitCommandException>(() => git.RemoveAsync(worktree, Ct));
        Assert.True(File.Exists(Path.Combine(worktree.Path, "uncommitted.txt")));
    }

    [Fact]
    public async Task Refuses_to_delete_an_unmerged_branch()
    {
        using var remote = new TempGitRemote();
        var git = Create(remote);
        var worktree = await git.PrepareAsync(Issue103, Ct);
        TempGitRemote.Commit(worktree.Path, "work.txt");

        await Assert.ThrowsAsync<GitCommandException>(() => git.RemoveAsync(worktree, Ct));
        Assert.Contains("adapters/103-github-sync", TempGitRemote.Run(remote.Main, "branch", "--list"));
    }
}
