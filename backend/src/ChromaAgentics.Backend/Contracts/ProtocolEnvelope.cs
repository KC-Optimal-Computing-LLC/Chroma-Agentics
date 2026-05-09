namespace ChromaAgentics.Backend.Contracts;

public sealed class ProtocolEnvelope<TPayload>
{
    public string ProtocolVersion { get; init; } = "0.1";
    public required string MessageId { get; init; }
    public string? WorkspaceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string SessionId { get; init; }
    public required long Sequence { get; init; }
    public required string Name { get; init; }
    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public required DateTime Timestamp { get; init; }
    public required TPayload Payload { get; init; }
}
