namespace Factory.Cli.Hooks;

/// <summary>Routes <c>factory hook &lt;event&gt;</c> to the handler for that event.</summary>
public static class HookDispatcher
{
    private const string Usage = "Usage: factory hook <pre-tool-use|permission-request|stop|notification|permission-denied|session-end>";

    private static readonly Dictionary<string, Func<HookIo, int>> Handlers = new()
    {
        ["pre-tool-use"] = PreToolUseHook.Run,
        ["permission-request"] = PermissionRequestHook.Run,
        ["stop"] = StopHook.Run,
        ["notification"] = NotificationHook.Run,
        ["permission-denied"] = PermissionDeniedHook.Run,
        ["session-end"] = SessionEndHook.Run,
    };

    public static int Run(string[] args, HookIo io)
    {
        if (args is not ["hook", var name] || !Handlers.TryGetValue(name, out var handler))
        {
            io.Stderr.WriteLine(Usage);
            return 2;
        }

        return handler(io);
    }
}
