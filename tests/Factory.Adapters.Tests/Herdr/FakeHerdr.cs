using Factory.Adapters.Shell;

namespace Factory.Adapters.Tests.Herdr;

/// <summary>Stands in for the herdr CLI: records every call and replies from a queue. Makes no real calls.</summary>
internal sealed class FakeHerdr : ICommandRunner
{
    private readonly Queue<ProcessResult> _replies = new();

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public FakeHerdr Replies(string stdOut) { _replies.Enqueue(new ProcessResult(0, stdOut, "")); return this; }

    public FakeHerdr Fails(string code, string message = "boom")
    {
        _replies.Enqueue(new ProcessResult(1, "", $$"""{"error":{"code":"{{code}}","message":"{{message}}"},"id":"x"}"""));
        return this;
    }

    public Task<ProcessResult> RunAsync(
        string command, IReadOnlyList<string> args, string? workingDirectory, CancellationToken ct)
    {
        Assert.Equal("herdr", command);
        Calls.Add(args);
        return Task.FromResult(_replies.Count > 0 ? _replies.Dequeue() : new ProcessResult(0, "", ""));
    }

    public const string WorkspaceList = """
        {"id":"x","result":{"type":"workspace_list","workspaces":[
          {"label":"pi-software-factory","workspace_id":"w1"},{"label":"factory","workspace_id":"w2"}]}}
        """;

    public const string WorkspaceListWithoutFactory = """
        {"id":"x","result":{"type":"workspace_list","workspaces":[{"label":"other","workspace_id":"w1"}]}}
        """;

    public const string WorkspaceCreated = """
        {"id":"x","result":{"type":"workspace_created","workspace":{"label":"factory","workspace_id":"w7"},
         "tab":{"tab_id":"w7:t1","workspace_id":"w7"},"root_pane":{"pane_id":"w7:p1"}}}
        """;

    public const string TabCreated = """
        {"id":"x","result":{"type":"tab_created","tab":{"tab_id":"w2:t4","workspace_id":"w2"},"root_pane":{"pane_id":"w2:p5"}}}
        """;

    public const string AgentStarted = """
        {"id":"x","result":{"type":"agent_info","agent":{"agent":"claude","pane_id":"w2:p5","agent_status":"idle"}}}
        """;

    public const string PaneInfo = """
        {"id":"x","result":{"type":"pane_info","pane":{"pane_id":"w2:p5","agent":"claude"}}}
        """;

    public static string AgentInfoIn(string paneId) =>
        """{"id":"x","result":{"type":"agent_info","agent":{"agent":"claude","pane_id":"PANE","agent_status":"idle"}}}"""
            .Replace("PANE", paneId);
}
