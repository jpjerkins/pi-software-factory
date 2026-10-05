namespace Factory.Domain.Runs;

/// <summary>The raw <c>/usage</c> text and when it was read.</summary>
public sealed record UsageReading(string RawText, DateTimeOffset ReadAt);
