using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Cli.Tests.RunOnce.Fakes;

/// <summary>Reports the worker as Done on the first look.</summary>
public sealed class FakeRunStore : IRunStore
{
    public int Created { get; private set; }

    public List<Run> Saved { get; } = [];

    public Task CreateAsync(RunId run, CancellationToken ct)
    {
        Created++;
        return Task.CompletedTask;
    }

    public Task<WorkerResult?> ReadResultAsync(RunId run, CancellationToken ct) =>
        Task.FromResult<WorkerResult?>(new WorkerResult(ReportedStatus.Done, "ok", [], [], [], []));

    public Task<WorkerSignals> ReadSignalsAsync(RunId run, CancellationToken ct) =>
        Task.FromResult(new WorkerSignals(FakeClock.Start, false));

    public Task SaveAsync(Run run, CancellationToken ct)
    {
        Saved.Add(run);
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(RunId run, CancellationToken ct) => Task.CompletedTask;
}
