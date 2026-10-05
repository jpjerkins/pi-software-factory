using Factory.Application.Ports;

namespace Factory.Application.Tests.Fakes;

internal sealed class FakeFolderTrust(CallLog log) : IFolderTrust
{
    public List<string> Trusted { get; } = [];

    public Task EnsureTrustedAsync(string cloneRoot, CancellationToken ct)
    {
        log.Add("trust");
        Trusted.Add(cloneRoot);
        return Task.CompletedTask;
    }
}
