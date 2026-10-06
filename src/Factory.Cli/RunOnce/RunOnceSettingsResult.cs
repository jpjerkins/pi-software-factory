namespace Factory.Cli.RunOnce;

/// <summary>Either parsed settings or a message saying what was wrong with the arguments.</summary>
public sealed record RunOnceSettingsResult(RunOnceSettings? Settings, string? Error)
{
    public bool IsValid => Settings is not null;
}
