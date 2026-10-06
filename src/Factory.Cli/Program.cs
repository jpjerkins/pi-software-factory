using Factory.Cli.Hooks;

if (args is ["hook", ..])
{
    var io = new HookIo(
        Console.In,
        Console.Out,
        Console.Error,
        Environment.GetEnvironmentVariable,
        () => DateTimeOffset.UtcNow);
    return HookDispatcher.Run(args, io);
}

Console.Error.WriteLine("Usage: factory <command>");
return 1;
