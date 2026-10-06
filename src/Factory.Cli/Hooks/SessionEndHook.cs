using System.Text.Json;
using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>Marks the session as ended so the factory can tell. Must finish well inside Claude Code's 1.5 s limit.</summary>
public static class SessionEndHook
{
    public static int Run(HookIo io)
    {
        try
        {
            var input = ReadInput(io);
            var marker = new { endedAt = io.Now().UtcDateTime.ToString("o"), reason = input?.Text("reason"), sessionId = input?.Text("session_id") };
            File.WriteAllText(Path.Combine(HookEnvironment.RunDirectory(io.Env), RunFiles.SessionEnded), JsonSerializer.Serialize(marker));
        }
        catch (Exception e)
        {
            io.Stderr.WriteLine($"session-end: marker not written: {e.Message}");
        }

        return 0;
    }

    // The marker matters more than its details, so unusable input still gets one.
    private static HookInput? ReadInput(HookIo io)
    {
        try
        {
            return HookInput.Parse(io.Stdin.ReadToEnd());
        }
        catch (HookFailure)
        {
            return null;
        }
    }
}
