namespace Factory.Adapters.Herdr;

/// <summary>Where and how workers are started.</summary>
/// <param name="RunsRoot">Parent of the per-run directories.</param>
/// <param name="FactoryBinaryPath">The published <c>factory</c> binary the worker hooks call.</param>
/// <param name="GitConfigPath">The published <c>assets/worker/gitconfig</c>.</param>
/// <param name="PromptTemplatePath">The published <c>assets/worker/prompt.md.template</c>.</param>
public sealed record HerdrOptions(
    string RunsRoot,
    string FactoryBinaryPath,
    string GitConfigPath,
    string PromptTemplatePath,
    string WorkspaceName = "factory",
    string SlotLabel = "factory-slot-1",
    TimeSpan? AgentTimeout = null)
{
    public TimeSpan AgentStartTimeout => AgentTimeout ?? TimeSpan.FromSeconds(60);
}
