using System.Text.Json;
using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>Blocks the worker from stopping until result.json is valid. Ignores stop_hook_active on purpose.</summary>
public static class StopHook
{
    public static int Run(HookIo io)
    {
        var problem = Check(io);
        if (problem is null)
        {
            Log(io, "allow", null);
            return 0;
        }

        var reason = $"You cannot stop yet: {problem} Write result.json in your run directory as JSON shaped like {ResultCheck.Shape}.";
        io.Stdout.Write(JsonSerializer.Serialize(new { decision = "block", reason }));
        Log(io, "block", problem);
        return 0;
    }

    // Fails closed: anything unexpected is a reason to keep the worker going.
    private static string? Check(HookIo io)
    {
        try
        {
            var path = Path.Combine(HookEnvironment.RunDirectory(io.Env), RunFiles.Result);
            return File.Exists(path)
                ? ResultCheck.ProblemWith(File.ReadAllText(path))
                : $"{path} does not exist.";
        }
        catch (Exception e)
        {
            return $"the factory could not check result.json ({e.Message}).";
        }
    }

    private static void Log(HookIo io, string decision, string? reason)
    {
        try
        {
            new RunLog(HookEnvironment.RunDirectory(io.Env), io.Now).Activity("stop", null, decision, reason);
        }
        catch (Exception e)
        {
            io.Stderr.WriteLine($"stop: could not log: {e.Message}");
        }
    }
}
