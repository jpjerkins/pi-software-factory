using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>Records calls that auto mode's classifier denied. Output is ignored by Claude Code.</summary>
public static class PermissionDeniedHook
{
    public static int Run(HookIo io)
    {
        try
        {
            var input = HookInput.Parse(io.Stdin.ReadToEnd());
            new RunLog(HookEnvironment.RunDirectory(io.Env), io.Now).Append(RunFiles.Denials,
                ("tool", input.Text("tool_name")), ("reason", input.Text("reason")), ("source", "auto-mode"));
        }
        catch (Exception e)
        {
            io.Stderr.WriteLine($"permission-denied: not recorded: {e.Message}");
        }

        return 0;
    }
}
