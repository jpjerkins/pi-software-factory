using System.Text.Json;
using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>The factory runs unattended, so every permission prompt is answered with a denial.</summary>
public static class PermissionRequestHook
{
    private const string Message =
        "The factory runs unattended, so nobody can approve this. Work within your allowed tools, " +
        "or stop and report status \"blocked\" or \"needs_input\" in result.json, saying what you need.";

    public static int Run(HookIo io)
    {
        // Exit 2 is not honoured for this event, so the JSON is the only denial and always goes out.
        io.Stdout.Write(DenyJson());
        TryRecord(io);
        return 0;
    }

    private static void TryRecord(HookIo io)
    {
        try
        {
            var log = new RunLog(HookEnvironment.RunDirectory(io.Env), io.Now);
            var tool = HookInput.Parse(io.Stdin.ReadToEnd()).Text("tool_name");
            log.Append(RunFiles.Denials, ("event", "permission-request"), ("tool", tool), ("reason", Message));
            log.Activity("permission-request", tool, "deny", Message);
        }
        catch (Exception e)
        {
            io.Stderr.WriteLine($"permission-request: could not record the denial: {e.Message}");
        }
    }

    private static string DenyJson() =>
        JsonSerializer.Serialize(new
        {
            hookSpecificOutput = new
            {
                hookEventName = "PermissionRequest",
                decision = new { behavior = "deny", message = Message },
            },
        });
}
