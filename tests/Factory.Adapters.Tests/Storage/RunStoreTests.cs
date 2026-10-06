using System.Text.Json;
using Factory.Adapters.Storage;
using Factory.Domain.Issues;
using Factory.Domain.Runs;

namespace Factory.Adapters.Tests.Storage;

public sealed class RunStoreTests : IDisposable
{
    private static readonly RunId Id = new("20261004-1530-i103");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TempRunsRoot _root = new();
    private readonly RunStore _store;

    public RunStoreTests() => _store = new RunStore(_root.Path);

    public void Dispose() => _root.Dispose();

    private string InRun(string file) => System.IO.Path.Combine(_root.RunDir(Id.Value), file);

    private void WorkerWrites(string file, string content) => File.WriteAllText(InRun(file), content);

    // CreateAsync

    [Fact]
    public async Task Create_makes_the_run_directory()
    {
        await _store.CreateAsync(Id, Ct);

        Assert.True(Directory.Exists(_root.RunDir(Id.Value)));
    }

    [Fact]
    public async Task Create_fails_if_the_run_already_exists()
    {
        await _store.CreateAsync(Id, Ct);

        await Assert.ThrowsAsync<IOException>(() => _store.CreateAsync(Id, Ct));
    }

    // SaveAsync

    private static Run ARun() => new(
        Id,
        new IssueNumber(103),
        new Worktree("/home/x/wt", "lane/i103"),
        new WorkerSession("w2:p3", "issue-103", "5b0f3c2e-8f7a-4c55-9a53-1d2e3f4a5b6c"),
        new DateTimeOffset(2026, 10, 4, 15, 30, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 10, 4, 16, 0, 0, TimeSpan.Zero),
        WorkerOutcome.NeedsInput,
        new UsageReading("before", new DateTimeOffset(2026, 10, 4, 15, 29, 0, TimeSpan.Zero)),
        new UsageReading("after", new DateTimeOffset(2026, 10, 4, 16, 1, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Save_writes_run_json_in_snake_case_with_a_string_outcome()
    {
        await _store.CreateAsync(Id, Ct);

        await _store.SaveAsync(ARun(), Ct);

        using var doc = JsonDocument.Parse(File.ReadAllText(InRun(RunFiles.Run)));
        var root = doc.RootElement;
        Assert.Equal("20261004-1530-i103", root.GetProperty("run_id").GetString());
        Assert.Equal(103, root.GetProperty("issue").GetInt32());
        Assert.Equal("lane/i103", root.GetProperty("worktree").GetProperty("branch").GetString());
        Assert.Equal("needs_input", root.GetProperty("outcome").GetString());
        var session = root.GetProperty("session");
        Assert.Equal("w2:p3", session.GetProperty("pane_id").GetString());
        Assert.Equal("issue-103", session.GetProperty("agent_name").GetString());
        Assert.Equal("5b0f3c2e-8f7a-4c55-9a53-1d2e3f4a5b6c", session.GetProperty("session_id").GetString());
        Assert.Equal("before", root.GetProperty("usage_before").GetProperty("raw_text").GetString());
        Assert.True(root.TryGetProperty("started_at", out _));
        Assert.True(root.TryGetProperty("ended_at", out _));
    }

    // ReadResultAsync

    [Fact]
    public async Task ReadResult_parses_a_worker_written_result()
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Result, """
            {"status":"plan_ready","summary":"s","tests_run":["t1"],"files_touched":["a.cs","b.cs"],
             "questions":["q?"],"problems_found":["p"]}
            """);

        var result = await _store.ReadResultAsync(Id, Ct);

        Assert.NotNull(result);
        Assert.Equal(ReportedStatus.PlanReady, result.Status);
        Assert.Equal("s", result.Summary);
        Assert.Equal(["t1"], result.TestsRun);
        Assert.Equal(["a.cs", "b.cs"], result.FilesTouched);
        Assert.Equal(["q?"], result.Questions);
        Assert.Equal(["p"], result.ProblemsFound);
    }

    [Theory]
    [InlineData("done", ReportedStatus.Done)]
    [InlineData("needs_input", ReportedStatus.NeedsInput)]
    [InlineData("blocked", ReportedStatus.Blocked)]
    public async Task ReadResult_maps_each_allowed_status(string text, ReportedStatus expected)
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Result, $$"""{"status":"{{text}}","summary":"s"}""");

        var result = await _store.ReadResultAsync(Id, Ct);

        Assert.Equal(expected, result?.Status);
        Assert.Empty(result!.TestsRun);
    }

    [Fact]
    public async Task ReadResult_is_null_when_missing()
    {
        await _store.CreateAsync(Id, Ct);

        Assert.Null(await _store.ReadResultAsync(Id, Ct));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{\"status\":\"done\",\"summ")]
    [InlineData("not json")]
    [InlineData("{\"status\":\"crashed\",\"summary\":\"s\"}")]
    [InlineData("{\"status\":\"stuck\",\"summary\":\"s\"}")]
    [InlineData("{\"status\":\"Done\",\"summary\":\"s\"}")]
    [InlineData("{\"status\":3,\"summary\":\"s\"}")]
    [InlineData("{\"summary\":\"s\"}")]
    public async Task ReadResult_is_null_when_unparseable_or_status_not_allowed(string content)
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Result, content);

        Assert.Null(await _store.ReadResultAsync(Id, Ct));
    }

    // ReadSignalsAsync

    private static readonly DateTime T1 = new(2026, 10, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Signals_last_activity_is_newest_of_activity_notifications_and_result()
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Activity, "x");
        WorkerWrites(RunFiles.Notifications, "x");
        WorkerWrites(RunFiles.Result, "x");
        File.SetLastWriteTimeUtc(InRun(RunFiles.Activity), T1);
        File.SetLastWriteTimeUtc(InRun(RunFiles.Notifications), T1.AddMinutes(10));
        File.SetLastWriteTimeUtc(InRun(RunFiles.Result), T1.AddMinutes(5));

        var signals = await _store.ReadSignalsAsync(Id, Ct);

        Assert.Equal(new DateTimeOffset(T1.AddMinutes(10)), signals.LastActivity);
    }

    [Fact]
    public async Task Signals_ignore_denials_and_run_files()
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Activity, "x");
        WorkerWrites(RunFiles.Denials, "x");
        File.SetLastWriteTimeUtc(InRun(RunFiles.Activity), T1);
        File.SetLastWriteTimeUtc(InRun(RunFiles.Denials), T1.AddHours(1));

        var signals = await _store.ReadSignalsAsync(Id, Ct);

        Assert.Equal(new DateTimeOffset(T1), signals.LastActivity);
    }

    [Fact]
    public async Task Signals_without_any_activity_file_fall_back_to_when_the_run_was_created()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        await _store.CreateAsync(Id, Ct);

        var signals = await _store.ReadSignalsAsync(Id, Ct);

        Assert.InRange(signals.LastActivity, before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Signals_session_ended_only_when_the_marker_exists()
    {
        await _store.CreateAsync(Id, Ct);
        Assert.False((await _store.ReadSignalsAsync(Id, Ct)).SessionEnded);

        WorkerWrites(RunFiles.SessionEnded, "{}");

        Assert.True((await _store.ReadSignalsAsync(Id, Ct)).SessionEnded);
    }

    // CleanUpAsync

    [Fact]
    public async Task CleanUp_moves_the_run_to_archive_keeping_its_files()
    {
        await _store.CreateAsync(Id, Ct);
        WorkerWrites(RunFiles.Activity, "kept");

        await _store.CleanUpAsync(Id, Ct);

        Assert.False(Directory.Exists(_root.RunDir(Id.Value)));
        var archived = System.IO.Path.Combine(_root.Path, RunFiles.ArchiveDirectory, Id.Value, RunFiles.Activity);
        Assert.Equal("kept", File.ReadAllText(archived));
    }

    [Fact]
    public async Task CleanUp_fails_if_the_archive_target_exists_and_leaves_the_run_alone()
    {
        await _store.CreateAsync(Id, Ct);
        Directory.CreateDirectory(System.IO.Path.Combine(_root.Path, RunFiles.ArchiveDirectory, Id.Value));

        await Assert.ThrowsAsync<IOException>(() => _store.CleanUpAsync(Id, Ct));

        Assert.True(Directory.Exists(_root.RunDir(Id.Value)));
    }

    [Fact]
    public async Task CleanUp_fails_if_the_run_is_missing() =>
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _store.CleanUpAsync(Id, Ct));
}
