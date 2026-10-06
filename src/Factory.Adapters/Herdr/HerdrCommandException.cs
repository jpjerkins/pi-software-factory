namespace Factory.Adapters.Herdr;

/// <summary>A herdr command failed. <see cref="Code"/> is herdr's error code (such as <c>agent_not_ready</c>), or empty if none was given.</summary>
public sealed class HerdrCommandException(string what, string code, string detail)
    : Exception($"herdr {what} failed{(code.Length > 0 ? $" ({code})" : "")}: {detail}")
{
    public string Code { get; } = code;
}
