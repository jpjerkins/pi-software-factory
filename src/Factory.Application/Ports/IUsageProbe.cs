using Factory.Domain.Runs;

namespace Factory.Application.Ports;

public interface IUsageProbe
{
    Task<UsageReading> ReadAsync(CancellationToken ct);
}
