using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeWorkerSlots(CallLog log) : IWorkerSlots
{
    public List<WorkerLaunch> Launches { get; } = [];

    /// <summary>What <see cref="IsRunningAsync"/> reports.</summary>
    public bool Running { get; set; } = true;

    public Task<WorkerSession> StartAsync(WorkerLaunch launch, CancellationToken ct)
    {
        log.Add("start");
        Launches.Add(launch);
        return Task.FromResult(new WorkerSession("w1:p1", "issue-103", "00000000-0000-0000-0000-000000000001"));
    }

    public Task<bool> IsRunningAsync(WorkerSession session, CancellationToken ct) => Task.FromResult(Running);
}
