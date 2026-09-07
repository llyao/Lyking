namespace Lyking.AiWorkflow.Routing;

public sealed record RouteRequest(string Text, IReadOnlyList<string> Paths);
public sealed record AreaCandidate(string Area, int Score, IReadOnlyList<string> Reasons);
public sealed record RiskMatch(string Id, string Description, string Reason);
public sealed record RouteResult(string? PrimaryArea, IReadOnlyList<string> RelatedAreas, IReadOnlyList<AreaCandidate> Candidates,
    IReadOnlyList<string> Contexts, IReadOnlyList<RiskMatch> RiskSignals, string SuggestedProfile, IReadOnlyList<string> ExcludedPaths);
