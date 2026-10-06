using Factory.Application.Ports;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeClock : IClock
{
    public static readonly DateTimeOffset Start = new(2026, 10, 5, 12, 30, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; private set; } = Start;

    public Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Now += delay;
        return Task.CompletedTask;
    }
}
