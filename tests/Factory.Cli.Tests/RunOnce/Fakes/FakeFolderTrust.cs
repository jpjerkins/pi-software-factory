using Factory.Application.Ports;

namespace Factory.Cli.Tests.RunOnce.Fakes;

public sealed class FakeFolderTrust : IFolderTrust
{
    public int Calls { get; private set; }

    public Task EnsureTrustedAsync(string repoRoot, CancellationToken ct)
    {
        Calls++;
        return Task.CompletedTask;
    }
}
