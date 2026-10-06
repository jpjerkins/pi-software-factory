using Factory.Adapters.Time;

namespace Factory.Adapters.Tests.Time;

public class SystemClockTests
{
    [Fact]
    public void Now_is_utc_and_close_to_the_real_time()
    {
        var before = DateTimeOffset.UtcNow;

        var now = new SystemClock().Now;

        Assert.Equal(TimeSpan.Zero, now.Offset);
        Assert.InRange(now, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task WaitAsync_throws_when_already_cancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SystemClock().WaitAsync(TimeSpan.FromMilliseconds(50), cts.Token));
    }

    [Fact]
    public async Task WaitAsync_completes_after_a_short_delay()
    {
        await new SystemClock().WaitAsync(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
    }
}
