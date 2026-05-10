namespace ChromaAgentics.Backend.Persistence.Entities;

public sealed class ExecutionEvent
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid WorkflowId { get; set; }
    public Guid? SessionId { get; set; }
    public long Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProtocolVersion { get; set; } = "0.2";
    public Guid MessageId { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? CausationMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? PayloadHash { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Workspace? Workspace { get; set; }
    public WorkflowExecution? Workflow { get; set; }
    public WorkflowSession? Session { get; set; }
}
