using System.Text.Json;
using Factory.Adapters.Claude;
using Factory.Adapters.Tests.Storage;

namespace Factory.Adapters.Tests.Claude;

public sealed class WorkerSettingsFileTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly TempRunsRoot _root = new();

    public void Dispose() => _root.Dispose();

    private async Task<JsonElement> WrittenHooksAsync(string binary = "/opt/factory/factory")
    {
        var path = await WorkerSettingsFile.WriteAsync(_root.Path, binary, Ct);
        Assert.Equal(Path.Combine(_root.Path, "worker-settings.json"), path);
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("hooks");
    }

    [Theory]
    [InlineData("PreToolUse", "pre-tool-use")]
    [InlineData("PermissionRequest", "permission-request")]
    [InlineData("Stop", "stop")]
    [InlineData("Notification", "notification")]
    [InlineData("SessionEnd", "session-end")]
    [InlineData("PermissionDenied", "permission-denied")]
    public async Task Each_event_runs_the_factory_hook_command(string evt, string kebab)
    {
        var hooks = await WrittenHooksAsync();

        var command = hooks.GetProperty(evt)[0].GetProperty("hooks")[0];
        Assert.Equal("command", command.GetProperty("type").GetString());
        Assert.Equal($"\"/opt/factory/factory\" hook {kebab}", command.GetProperty("command").GetString());
    }

    [Fact]
    public async Task Pre_tool_use_matches_every_tool()
    {
        var hooks = await WrittenHooksAsync();

        Assert.Equal("*", hooks.GetProperty("PreToolUse")[0].GetProperty("matcher").GetString());
    }

    [Fact]
    public async Task Only_the_six_events_are_hooked()
    {
        var hooks = await WrittenHooksAsync();

        Assert.Equal(6, hooks.EnumerateObject().Count());
    }
}
