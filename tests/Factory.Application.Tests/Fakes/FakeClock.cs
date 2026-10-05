using Factory.Application.Ports;

namespace Factory.Application.Tests.Fakes;

/// <summary>A clock whose waits finish instantly but move time forward.</summary>
internal sealed class FakeClock(DateTimeOffset start) : IClock
{
    public DateTimeOffset Now { get; private set; } = start;

    public int Waits { get; private set; }

    public Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        Waits++;
        Now += delay;
        return Task.CompletedTask;
    }
}
