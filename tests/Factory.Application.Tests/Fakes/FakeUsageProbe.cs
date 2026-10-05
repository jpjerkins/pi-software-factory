using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

/// <summary>Returns "usage-1", "usage-2", ... on successive reads, stamped with the clock.</summary>
internal sealed class FakeUsageProbe(FakeClock clock) : IUsageProbe
{
    public int Reads { get; private set; }

    public Task<UsageReading> ReadAsync(CancellationToken ct) =>
        Task.FromResult(new UsageReading($"usage-{++Reads}", clock.Now));
}
