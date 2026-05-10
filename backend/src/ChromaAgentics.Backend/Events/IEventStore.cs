using ChromaAgentics.Backend.Persistence.Entities;

namespace ChromaAgentics.Backend.Events;

public sealed record AppendEventRequest(
    Guid WorkspaceId,
    Guid WorkflowId,
    Guid? SessionId,
    string Name,
    string ProtocolVersion,
    Guid MessageId,
    Guid? CorrelationId,
    Guid? CausationMessageId,
    string? IdempotencyKey,
    string? PayloadHash,
    string PayloadJson);

public interface IEventStore
{
    Task<ExecutionEvent> AppendEventAsync(AppendEventRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExecutionEvent>> GetEventsAfterSequenceAsync(
        Guid workflowId,
        long lastSeenSequence,
        CancellationToken cancellationToken);

    Task<long> GetMaxSequenceAsync(Guid workflowId, CancellationToken cancellationToken);

    Task<ExecutionEvent?> GetEventByIdempotencyKeyAsync(
        Guid workflowId,
        string name,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ExecutionEvent?> GetEventByMessageIdAsync(
        Guid workflowId,
        Guid messageId,
        CancellationToken cancellationToken);
}
