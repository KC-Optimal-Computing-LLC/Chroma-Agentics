namespace ChromaAgentics.Backend.Acknowledgements;

public sealed record AcknowledgementUpdateResult(long PreviousLastSeenSequence, long LastSeenSequence, bool Updated);

public interface IAcknowledgementStore
{
    Task<long> GetLastSeenSequenceAsync(
        Guid workflowId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<AcknowledgementUpdateResult> UpdateLastSeenSequenceAsync(
        Guid workspaceId,
        Guid workflowId,
        Guid sessionId,
        long lastSeenSequence,
        CancellationToken cancellationToken);
}
