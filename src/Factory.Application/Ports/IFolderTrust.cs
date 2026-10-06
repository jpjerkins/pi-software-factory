namespace Factory.Application.Ports;

public interface IFolderTrust
{
    /// <summary>Trusts exactly <paramref name="folder"/>; a trusted parent does not cover folders below it.</summary>
    Task EnsureTrustedAsync(string folder, CancellationToken ct);
}
