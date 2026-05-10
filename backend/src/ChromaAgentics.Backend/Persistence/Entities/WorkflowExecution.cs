namespace ChromaAgentics.Backend.Persistence.Entities;

public sealed class WorkflowExecution
{
    public const string StatusCreated = "created";
    public const string StatusRunning = "running";
    public const string StatusCancelled = "cancelled";
    public const string StatusCompleted = "completed";
    public const string StatusFailed = "failed";

    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public string Status { get; set; } = StatusCreated;
    public string? Title { get; set; }
    public string? Mode { get; set; }
    public string? Source { get; set; }
    public long NextSequence { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public Workspace? Workspace { get; set; }
}
