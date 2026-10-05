using Factory.Domain.Runs;

namespace Factory.Application.Ports;

public interface IRunStore
{
    Task CreateAsync(RunId run, CancellationToken ct);

    Task<WorkerResult?> ReadResultAsync(RunId run, CancellationToken ct);

    Task<WorkerSignals> ReadSignalsAsync(RunId run, CancellationToken ct);

    Task SaveAsync(Run run, CancellationToken ct);

    /// <summary>v1: archive the run (move to runs/archive/&lt;run-id&gt;/), never delete.</summary>
    Task CleanUpAsync(RunId run, CancellationToken ct);
}
