using System.Text.Json;

namespace ChromaAgentics.Backend.Protocol;

public sealed class ProtocolErrorFactory(TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ProtocolEnvelope Create(
        string code,
        string message,
        ProtocolEnvelope? request = null,
        bool retryable = false)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = TryNormalizeGuid(request?.WorkspaceId),
            WorkflowId = TryNormalizeGuid(request?.WorkflowId),
            SessionId = TryNormalizeGuid(request?.SessionId),
            Sequence = null,
            Name = ProtocolEventNames.Error,
            CorrelationId = TryNormalizeGuid(request?.CorrelationId),
            IdempotencyKey = null,
            Timestamp = timeProvider.GetUtcNow(),
            Payload = JsonSerializer.SerializeToElement(new
            {
                code,
                message = Sanitize(message),
                retryable
            }, JsonOptions)
        };
    }

    public ProtocolEnvelope FromValidation(ProtocolValidationResult validation, ProtocolEnvelope request)
    {
        return Create(
            validation.ErrorCode ?? "internal_error",
            validation.ErrorMessage ?? "The protocol message could not be processed.",
            request);
    }

    private static string? TryNormalizeGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed.ToString("D") : null;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "The protocol message could not be processed.";
        }

        return value.Length > 240 ? value[..240] : value;
    }
}
