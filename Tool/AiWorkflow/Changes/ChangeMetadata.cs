namespace Lyking.AiWorkflow.Changes;

public enum WorkProfile { Direct, Standard, Controlled }
public enum WorkflowStatus { Draft, Ready, Active, Blocked, Verified, Archived }
public sealed record ChangeMetadata(int SchemaVersion, string Id, WorkProfile Profile, WorkflowStatus Status, List<string> Areas, List<string> Paths, List<string> RiskSignals);
