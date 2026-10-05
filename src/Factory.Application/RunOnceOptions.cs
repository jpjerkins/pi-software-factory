namespace Factory.Application;

public sealed record RunOnceOptions(string CloneRoot, TimeSpan PollInterval, TimeSpan StuckTimeout);
