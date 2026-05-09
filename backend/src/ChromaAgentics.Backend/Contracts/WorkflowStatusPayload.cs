namespace ChromaAgentics.Backend.Contracts;

public sealed class WorkflowStatusPayload
{
    public required string Status { get; init; }
    public string? Detail { get; init; }
}
