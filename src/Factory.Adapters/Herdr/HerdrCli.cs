using System.Text.Json;
using Factory.Adapters.Shell;

namespace Factory.Adapters.Herdr;

/// <summary>Runs herdr commands and returns the <c>result</c> object of its JSON reply.</summary>
internal sealed class HerdrCli(ICommandRunner runner)
{
    public async Task<JsonElement> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var what = string.Join(' ', args.Take(2));
        var reply = await runner.RunAsync("herdr", args, null, ct);
        var output = reply.StdOut.Length > 0 && reply.ExitCode == 0 ? reply.StdOut : $"{reply.StdOut}{reply.StdErr}".Trim();

        if (reply.ExitCode != 0)
        {
            var (code, message) = ErrorIn(output);
            throw new HerdrCommandException(what, code, message ?? $"exit {reply.ExitCode}: {output}");
        }

        try
        {
            using var doc = JsonDocument.Parse(output);
            return doc.RootElement.GetProperty("result").Clone();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new HerdrCommandException(what, "", $"unexpected reply: {output}");
        }
    }

    private static (string Code, string? Message) ErrorIn(string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            var error = doc.RootElement.GetProperty("error");
            return (error.GetProperty("code").GetString() ?? "", error.GetProperty("message").GetString());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return ("", null);
        }
    }
}
