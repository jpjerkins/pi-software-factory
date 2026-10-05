using System.Text.Json.Nodes;
using Factory.Adapters.Claude;

namespace Factory.Adapters.Tests.Claude;

public sealed class ClaudeTrustTests : IDisposable
{
    private const string Clone = "/home/x/dev/factory/repo";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private readonly TempConfigDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private static JsonNode Parse(string file) => JsonNode.Parse(File.ReadAllText(file))!;

    [Fact]
    public async Task Adds_entry_when_project_is_missing()
    {
        var file = _dir.Write("""{"projects":{}}""");

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        Assert.True((bool)Parse(file)["projects"]![Clone]!["hasTrustDialogAccepted"]!);
    }

    [Fact]
    public async Task Adds_projects_object_when_absent()
    {
        var file = _dir.Write("""{"theme":"dark"}""");

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        Assert.True((bool)Parse(file)["projects"]![Clone]!["hasTrustDialogAccepted"]!);
    }

    [Fact]
    public async Task Keeps_other_keys_of_existing_project_entry()
    {
        var file = _dir.Write("""{"projects":{"CLONE":{"allowedTools":["a"],"hasTrustDialogAccepted":false,"n":3}}}""".Replace("CLONE", Clone));

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        var entry = Parse(file)["projects"]![Clone]!;
        Assert.True((bool)entry["hasTrustDialogAccepted"]!);
        Assert.Equal("a", (string)entry["allowedTools"]![0]!);
        Assert.Equal(3, (int)entry["n"]!);
    }

    [Fact]
    public async Task Leaves_other_projects_and_top_level_keys_untouched()
    {
        var file = _dir.Write("""{"numStartups":7,"projects":{"/other":{"hasTrustDialogAccepted":false,"x":[1,2]}},"tail":{"a":null}}""");

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        var root = Parse(file);
        Assert.Equal(7, (int)root["numStartups"]!);
        Assert.Equal("""{"hasTrustDialogAccepted":false,"x":[1,2]}""", root["projects"]!["/other"]!.ToJsonString());
        Assert.Equal("""{"a":null}""", root["tail"]!.ToJsonString());
    }

    [Fact]
    public async Task Does_not_write_when_already_trusted()
    {
        var file = _dir.Write("""{ "projects": { "CLONE": { "hasTrustDialogAccepted": true } } }""".Replace("CLONE", Clone));
        var before = File.ReadAllText(file);
        var oldTime = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, oldTime);

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        Assert.Equal(before, File.ReadAllText(file));
        Assert.Equal(oldTime, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Preserves_file_permissions()
    {
        var file = _dir.Write("""{"projects":{}}""");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    [Fact]
    public async Task Leaves_no_temp_files_behind()
    {
        var file = _dir.Write("""{"projects":{}}""");

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct);

        Assert.Equal([file], Directory.GetFiles(_dir.Path));
    }

    [Fact]
    public async Task Trailing_slash_is_normalised()
    {
        var file = _dir.Write("""{"projects":{}}""");

        await new ClaudeTrust(file).EnsureTrustedAsync(Clone + "/", Ct);

        var projects = (JsonObject)Parse(file)["projects"]!;
        Assert.Equal([Clone], projects.Select(p => p.Key));
    }

    [Fact]
    public async Task Invalid_json_throws_and_leaves_file_as_is()
    {
        var file = _dir.Write("{ not json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct));

        Assert.Equal("{ not json", File.ReadAllText(file));
    }

    [Fact]
    public async Task Missing_file_throws_and_creates_nothing()
    {
        var file = Path.Combine(_dir.Path, "absent.json");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new ClaudeTrust(file).EnsureTrustedAsync(Clone, Ct));

        Assert.Empty(Directory.GetFiles(_dir.Path));
    }
}
