using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeUsageProbe : IUsageProbe
{
    public Task<UsageReading> ReadAsync(CancellationToken ct) =>
        Task.FromResult(new UsageReading("usage", FakeClock.Start));
}
