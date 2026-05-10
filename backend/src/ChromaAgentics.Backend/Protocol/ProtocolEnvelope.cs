using System.Text.Json;
using System.Text.Json.Serialization;
using ChromaAgentics.Backend.Persistence.Entities;

namespace ChromaAgentics.Backend.Protocol;

public sealed class ProtocolEnvelope
{
    public string? ProtocolVersion { get; init; }
    public string? MessageId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? WorkflowId { get; init; }
    public string? SessionId { get; init; }
    public long? Sequence { get; init; }
    public string? Name { get; init; }
    public string? CorrelationId { get; init; }
    public string? IdempotencyKey { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public JsonElement Payload { get; init; }

    [JsonIgnore]
    public bool HasPayload => Payload.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;

    public static ProtocolEnvelope FromExecutionEvent(ExecutionEvent executionEvent)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = executionEvent.ProtocolVersion,
            MessageId = executionEvent.MessageId.ToString("D"),
            WorkspaceId = executionEvent.WorkspaceId.ToString("D"),
            WorkflowId = executionEvent.WorkflowId.ToString("D"),
            SessionId = executionEvent.SessionId?.ToString("D"),
            Sequence = executionEvent.Sequence,
            Name = executionEvent.Name,
            CorrelationId = executionEvent.CorrelationId?.ToString("D"),
            IdempotencyKey = executionEvent.IdempotencyKey,
            Timestamp = executionEvent.CreatedAtUtc,
            Payload = JsonSerializer.Deserialize<JsonElement>(executionEvent.PayloadJson)
        };
    }
}

public static class ProtocolEnvelopeFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProtocolEnvelope CreateNonDurable(
        string name,
        Guid? workspaceId,
        Guid? workflowId,
        Guid? sessionId,
        Guid? correlationId,
        object payload,
        TimeProvider timeProvider)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = workspaceId?.ToString("D"),
            WorkflowId = workflowId?.ToString("D"),
            SessionId = sessionId?.ToString("D"),
            Sequence = null,
            Name = name,
            CorrelationId = correlationId?.ToString("D"),
            IdempotencyKey = null,
            Timestamp = timeProvider.GetUtcNow(),
            Payload = JsonSerializer.SerializeToElement(payload, JsonOptions)
        };
    }
}
