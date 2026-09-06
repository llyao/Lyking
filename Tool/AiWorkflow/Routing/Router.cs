using Lyking.AiWorkflow.Configuration;
using Lyking.AiWorkflow.Core;

namespace Lyking.AiWorkflow.Routing;

public sealed class Router(string root, WorkflowConfig config)
{
    public RouteResult Route(RouteRequest request) => Route(request.Text, request.Paths);

    public RouteResult Route(string? text, IEnumerable<string> paths)
    {
        var normalized = paths.Select(p => PathPolicy.Relative(root, p)).ToArray();
        var definite = normalized.Select(p => config.Areas.FirstOrDefault(a => PathPolicy.Contains(a.Root, p))?.Name)
            .Where(a => a is not null).Cast<string>().Distinct().ToArray();
        var primary = definite.FirstOrDefault();
        var related = config.Areas.Where(a => a.Name != primary && definite.Contains(a.Name)).Select(a => a.Name).ToArray();
        var candidates = config.Areas.Select(a =>
        {
            var reasons = normalized.Where(p => PathPolicy.Contains(a.Root, p)).Select(p => $"路径：{p}").ToList();
            var score = reasons.Count > 0 ? 100 : 0;
            foreach (var word in a.Keywords.Where(k => (text ?? "").Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                score++; reasons.Add($"文本关键词：{word}");
            }
            return new AreaCandidate(a.Name, score, reasons);
        }).Where(c => c.Score > 0).OrderByDescending(c => c.Score).ToArray();
        var risks = new List<RiskMatch>();
        foreach (var rule in config.RiskRules)
        {
            var matched = normalized.Where(p => rule.PathPrefixes.Any(prefix => PathPolicy.Contains(prefix, p)) ||
                rule.PathSegments.Any(segment => p.Split('/').Contains(segment, StringComparer.OrdinalIgnoreCase))).ToArray();
            if (matched.Length > 0) risks.Add(new(rule.Id, rule.Description, string.Join("、", matched)));
        }
        if (definite.Length >= 3) risks.Add(new("multi-area", "跨三个以上领域", string.Join("、", definite)));
        var contexts = new List<string> { "CONTEXT-MAP.md" };
        foreach (var name in new[] { primary }.Concat(related).Where(n => n is not null))
            contexts.Add(config.Areas.Single(a => a.Name == name).Context);
        return new(primary, related, candidates, contexts, risks, risks.Count > 0 ? "controlled" : "standard", config.ExcludedPaths);
    }
}
