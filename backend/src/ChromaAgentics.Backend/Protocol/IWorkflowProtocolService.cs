namespace ChromaAgentics.Backend.Protocol;

public sealed record WorkflowProtocolResult(IReadOnlyList<ProtocolEnvelope> Envelopes);

public interface IWorkflowProtocolService
{
    Task<WorkflowProtocolResult> StartWorkflowAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken);

    Task<WorkflowProtocolResult> ResumeSessionAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken);

    Task<WorkflowProtocolResult> AcknowledgeEventsAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken);
}
