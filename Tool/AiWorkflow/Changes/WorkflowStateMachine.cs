namespace Lyking.AiWorkflow.Changes;

public static class WorkflowStateMachine
{
    private static readonly IReadOnlySet<(WorkflowStatus, WorkflowStatus)> Allowed = new HashSet<(WorkflowStatus, WorkflowStatus)>
    {
        (WorkflowStatus.Draft, WorkflowStatus.Ready), (WorkflowStatus.Ready, WorkflowStatus.Active),
        (WorkflowStatus.Active, WorkflowStatus.Blocked), (WorkflowStatus.Blocked, WorkflowStatus.Active),
        (WorkflowStatus.Active, WorkflowStatus.Verified), (WorkflowStatus.Verified, WorkflowStatus.Archived)
    };

    public static bool CanTransition(string from, string to) =>
        ChangeDocumentParser.Statuses.TryGetValue(from, out var a) &&
        ChangeDocumentParser.Statuses.TryGetValue(to, out var b) && Allowed.Contains((a, b));
}
