namespace Factory.Application.Ports;

public interface IFolderTrust
{
    Task EnsureTrustedAsync(string repoRoot, CancellationToken ct);
}
