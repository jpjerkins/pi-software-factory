namespace Factory.Application.Tests.Fakes;

/// <summary>Shared, ordered record of calls made across all fakes.</summary>
internal sealed class CallLog
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls => _calls;

    public void Add(string call) => _calls.Add(call);

    public int IndexOf(string call) => _calls.IndexOf(call);
}
