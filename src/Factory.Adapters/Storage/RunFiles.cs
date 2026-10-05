namespace Factory.Adapters.Storage;

/// <summary>File and directory names inside a run directory. Shared contract with the worker hooks.</summary>
public static class RunFiles
{
    public const string Run = "run.json";
    public const string Result = "result.json";
    public const string Activity = "activity.log";
    public const string Notifications = "notifications.log";
    public const string Denials = "denials.log";
    public const string SessionEnded = "session-ended.json";

    /// <summary>Sibling of the run directories that holds archived runs.</summary>
    public const string ArchiveDirectory = "archive";
}
