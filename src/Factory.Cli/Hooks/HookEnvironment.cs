namespace Factory.Cli.Hooks;

/// <summary>The environment the factory gives each worker, read through an injectable lookup.</summary>
public sealed record HookEnvironment(string RunDir, string Worktree, string Home)
{
    public static string RunDirectory(Func<string, string?> env) => Require(env, "FACTORY_RUN_DIR");

    public static HookEnvironment Read(Func<string, string?> env) =>
        new(RunDirectory(env), Require(env, "FACTORY_WORKTREE"), Require(env, "HOME"));

    private static string Require(Func<string, string?> env, string name) =>
        env(name) is { Length: > 0 } value ? value : throw new HookFailure($"{name} is not set.");
}
