namespace Lyking.AiWorkflow.Configuration;

public sealed class WorkflowConfig
{
    public int SchemaVersion { get; init; }
    public required string WorkflowVersion { get; init; }
    public required List<AreaDefinition> Areas { get; init; }
    public required List<RiskRule> RiskRules { get; init; }
    public required Dictionary<string, List<string>> Profiles { get; init; }
    public required ChangeDirectories Changes { get; init; }
    public required AdapterDirectories Adapters { get; init; }
    public required List<string> ExcludedPaths { get; init; }
}
public sealed record AreaDefinition(string Name, string Root, string Context, List<string> Keywords);
public sealed record RiskRule(string Id, string Description, List<string> PathPrefixes, List<string> PathSegments);
public sealed record ChangeDirectories(string Active, string Archive);
public sealed record AdapterDirectories(string ClaudeSkills, string CodexAgents, string ClaudeAgents, string GeminiAgents, string Manifest);
