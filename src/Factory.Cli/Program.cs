using Factory.Cli;
using Factory.Cli.Hooks;
using Factory.Cli.RunOnce;

var router = new CommandRouter(RunHook, RunOnceAsync, UserHome(), Console.Error);
return await router.RunAsync(args);

static string UserHome() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

int RunHook(string[] hookArgs)
{
    var io = new HookIo(
        Console.In,
        Console.Out,
        Console.Error,
        Environment.GetEnvironmentVariable,
        () => DateTimeOffset.UtcNow);
    return HookDispatcher.Run(hookArgs, io);
}

static async Task<int> RunOnceAsync(RunOnceSettings settings)
{
    using var cancel = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancel.Cancel();
    };

    var wiring = new RunOnceWiring(settings, Environment.ProcessPath!, AppContext.BaseDirectory, UserHome(), Environment.GetEnvironmentVariable("DOTNET_ROOT"));
    var command = new RunOnceCommand(settings, wiring.RunOnce, wiring.NextIssue, Console.Out, Console.Error);
    return await command.RunAsync(cancel.Token);
}
