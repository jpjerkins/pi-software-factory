using Factory.Application.Ports;
using Factory.Domain.Runs;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeRunStore(FakeClock clock, CallLog log) : IRunStore
{
    public List<RunId> Created { get; } = [];
    public List<Run> Saved { get; } = [];
    public List<RunId> CleanedUp { get; } = [];

    /// <summary>The result the worker has written, if any. Tests set this directly.</summary>
    public WorkerResult? Result { get; set; }

    /// <summary>The signals the hooks have recorded. Defaults to "active right now".</summary>
    public WorkerSignals? Signals { get; set; }

    /// <summary>Runs after each poll, so a test can make the worker progress while the dispatcher waits.</summary>
    public Action<int>? OnRead { get; set; }

    private int _reads;

    public Task CreateAsync(RunId run, CancellationToken ct)
    {
        log.Add("create-run");
        Created.Add(run);
        return Task.CompletedTask;
    }

    public Task<WorkerResult?> ReadResultAsync(RunId run, CancellationToken ct)
    {
        OnRead?.Invoke(++_reads);
        return Task.FromResult(Result);
    }

    public Task<WorkerSignals> ReadSignalsAsync(RunId run, CancellationToken ct) =>
        Task.FromResult(Signals ?? new WorkerSignals(clock.Now, SessionEnded: false));

    public Task SaveAsync(Run run, CancellationToken ct)
    {
        log.Add("save-run");
        Saved.Add(run);
        return Task.CompletedTask;
    }

    public Task CleanUpAsync(RunId run, CancellationToken ct)
    {
        CleanedUp.Add(run);
        return Task.CompletedTask;
    }
}
