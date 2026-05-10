namespace ChromaAgentics.Backend.Persistence.Entities;

public sealed class WorkflowSession
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid WorkflowId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset LastConnectedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClientName { get; set; }

    public Workspace? Workspace { get; set; }
    public WorkflowExecution? Workflow { get; set; }
}
