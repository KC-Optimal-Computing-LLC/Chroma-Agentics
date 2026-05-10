namespace ChromaAgentics.Backend.Persistence.Entities;

public sealed class EventAcknowledgement
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid SessionId { get; set; }
    public long LastSeenSequence { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Workspace? Workspace { get; set; }
    public WorkflowExecution? Workflow { get; set; }
    public WorkflowSession? Session { get; set; }
}
