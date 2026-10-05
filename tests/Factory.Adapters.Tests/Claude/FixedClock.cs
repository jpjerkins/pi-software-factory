using Factory.Application.Ports;

namespace Factory.Adapters.Tests.Claude;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset Now { get; } = now;

    public Task WaitAsync(TimeSpan delay, CancellationToken ct) => Task.CompletedTask;
}
