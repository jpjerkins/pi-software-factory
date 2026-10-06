using Factory.Application.Ports;

namespace Factory.Adapters.Time;

/// <summary>The real clock: UTC wall time, and real delays.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public Task WaitAsync(TimeSpan delay, CancellationToken ct) => Task.Delay(delay, ct);
}
