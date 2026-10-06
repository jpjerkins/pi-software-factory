using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeWorkerSlots : IWorkerSlots
{
    public int Started { get; private set; }

    public Task<WorkerSession> StartAsync(WorkerLaunch launch, CancellationToken ct)
    {
        Started++;
        return Task.FromResult(new WorkerSession("pane-1", "agent-1", "session-1"));
    }

    public Task<bool> IsRunningAsync(WorkerSession session, CancellationToken ct) => Task.FromResult(true);
}
