namespace ChromaAgentics.Backend.Protocol;

public static class ProtocolEventNames
{
    public const string ProtocolVersion = "0.2";

    public const string WorkflowStart = "workflow.start";
    public const string SessionResume = "session.resume";
    public const string EventAck = "event.ack";

    public const string ConnectionReady = "connection.ready";
    public const string WorkflowStarted = "workflow.started";
    public const string WorkflowStatus = "workflow.status";
    public const string Error = "error";
}
