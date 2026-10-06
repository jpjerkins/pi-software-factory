using Factory.Adapters.Storage;

namespace Factory.Cli.Hooks;

/// <summary>Records notifications (their timing is a liveness signal). Never blocks.</summary>
public static class NotificationHook
{
    public static int Run(HookIo io)
    {
        try
        {
            var input = HookInput.Parse(io.Stdin.ReadToEnd());
            new RunLog(HookEnvironment.RunDirectory(io.Env), io.Now).Append(RunFiles.Notifications,
                ("type", input.Text("notification_type")), ("title", input.Text("title")), ("message", input.Text("message")));
        }
        catch (Exception e)
        {
            io.Stderr.WriteLine($"notification: not recorded: {e.Message}");
        }

        return 0;
    }
}
