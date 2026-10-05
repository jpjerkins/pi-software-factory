namespace Factory.Application.Ports;

public interface IFolderTrust
{
    Task EnsureTrustedAsync(string cloneRoot, CancellationToken ct);
}
