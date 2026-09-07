namespace Lyking.AiWorkflow.Core;

public sealed class WorkflowException : Exception
{
    public string Code { get; }
    public WorkflowException(string message, string code = "workflow.invalid") : base(message) => Code = code;
}
