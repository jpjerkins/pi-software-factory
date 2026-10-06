namespace Factory.Cli.Hooks;

/// <summary>Everything a hook touches outside the run directory, so tests can fake it.</summary>
public sealed record HookIo(
    TextReader Stdin,
    TextWriter Stdout,
    TextWriter Stderr,
    Func<string, string?> Env,
    Func<DateTimeOffset> Now);
