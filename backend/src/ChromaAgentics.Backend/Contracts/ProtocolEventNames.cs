namespace ChromaAgentics.Backend.Contracts;

public static class ProtocolEventNames
{
    public const string WorkflowStatus = "workflow.status";
    public const string ConnectionReady = "connection.ready";
    public const string Error = "error";

    // Future-facing contract names. These are not implemented in Sprint 1.
    public const string ApprovalRequest = "approval.request";
    public const string ApprovalDecision = "approval.decision";
    public const string EventAck = "event.ack";
}
