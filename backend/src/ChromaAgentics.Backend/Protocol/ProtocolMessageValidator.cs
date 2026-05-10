namespace ChromaAgentics.Backend.Protocol;

public sealed class ProtocolMessageValidator : IProtocolMessageValidator
{
    private static readonly HashSet<string> KnownInboundNames = new(StringComparer.Ordinal)
    {
        ProtocolEventNames.WorkflowStart,
        ProtocolEventNames.SessionResume,
        ProtocolEventNames.EventAck
    };

    public ProtocolValidationResult Validate(ProtocolEnvelope envelope)
    {
        if (envelope.ProtocolVersion != ProtocolEventNames.ProtocolVersion)
        {
            return ProtocolValidationResult.Error(
                "bad_protocol_version",
                "Protocol version 0.2 is required.");
        }

        if (string.IsNullOrWhiteSpace(envelope.Name))
        {
            return ProtocolValidationResult.Error("missing_required_field", "name is required.");
        }

        if (!KnownInboundNames.Contains(envelope.Name))
        {
            return ProtocolValidationResult.Error("unknown_message_name", "The message name is not implemented.");
        }

        if (!TryRequiredGuid(envelope.MessageId, "messageId", out var messageIdResult))
        {
            return messageIdResult;
        }

        if (!TryRequiredGuid(envelope.WorkspaceId, "workspaceId", out var workspaceIdResult))
        {
            return workspaceIdResult;
        }

        if (!TryRequiredGuid(envelope.SessionId, "sessionId", out var sessionIdResult))
        {
            return sessionIdResult;
        }

        if (envelope.Name is ProtocolEventNames.WorkflowStart or ProtocolEventNames.SessionResume or ProtocolEventNames.EventAck &&
            !TryRequiredGuid(envelope.WorkflowId, "workflowId", out var workflowIdResult))
        {
            return workflowIdResult;
        }

        if (!string.IsNullOrWhiteSpace(envelope.CorrelationId) && !Guid.TryParse(envelope.CorrelationId, out _))
        {
            return ProtocolValidationResult.Error("invalid_id", "correlationId must be a UUID when provided.");
        }

        if (envelope.Timestamp is null)
        {
            return ProtocolValidationResult.Error("missing_required_field", "timestamp is required.");
        }

        if (!envelope.HasPayload || envelope.Payload.ValueKind is not System.Text.Json.JsonValueKind.Object)
        {
            return ProtocolValidationResult.Error("missing_required_field", "payload object is required.");
        }

        return envelope.Name switch
        {
            ProtocolEventNames.SessionResume => ValidateLastSeenPayload(envelope, "lastSeenSequence"),
            ProtocolEventNames.EventAck => ValidateLastSeenPayload(envelope, "lastSeenSequence"),
            _ => ProtocolValidationResult.Valid
        };
    }

    private static ProtocolValidationResult ValidateLastSeenPayload(ProtocolEnvelope envelope, string fieldName)
    {
        if (!envelope.Payload.TryGetProperty(fieldName, out var property))
        {
            return ProtocolValidationResult.Error("missing_required_field", $"{fieldName} is required.");
        }

        if (!property.TryGetInt64(out var lastSeenSequence) || lastSeenSequence < 0)
        {
            return ProtocolValidationResult.Error("invalid_id", $"{fieldName} must be a non-negative integer.");
        }

        return ProtocolValidationResult.Valid;
    }

    private static bool TryRequiredGuid(string? value, string fieldName, out ProtocolValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = ProtocolValidationResult.Error("missing_required_field", $"{fieldName} is required.");
            return false;
        }

        if (!Guid.TryParse(value, out _))
        {
            result = ProtocolValidationResult.Error("invalid_id", $"{fieldName} must be a UUID.");
            return false;
        }

        result = ProtocolValidationResult.Valid;
        return true;
    }
}
