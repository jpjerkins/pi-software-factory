namespace Factory.Cli.Hooks;

/// <summary>A hook could not do its job (bad environment or input). Guarding hooks turn this into a denial.</summary>
public sealed class HookFailure(string message) : Exception(message);
