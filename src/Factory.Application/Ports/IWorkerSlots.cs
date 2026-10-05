using Factory.Domain.Runs;

namespace Factory.Application.Ports;

public interface IWorkerSlots
{
    Task<WorkerSession> StartAsync(WorkerLaunch launch, CancellationToken ct);

    Task<bool> IsRunningAsync(WorkerSession session, CancellationToken ct);
}
