namespace Factory.Application.Ports;

public interface IClock
{
    DateTimeOffset Now { get; }

    Task WaitAsync(TimeSpan delay, CancellationToken ct);
}
