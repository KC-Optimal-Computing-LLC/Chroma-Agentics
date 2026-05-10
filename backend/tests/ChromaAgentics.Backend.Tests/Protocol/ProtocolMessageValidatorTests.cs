using System.Text.Json;
using ChromaAgentics.Backend.Protocol;

namespace ChromaAgentics.Backend.Tests.Protocol;

public sealed class ProtocolMessageValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ProtocolMessageValidator validator = new();

    [Fact]
    public void Validate_AcceptsWorkflowStartEnvelope()
    {
        var result = validator.Validate(CreateEnvelope(ProtocolEventNames.WorkflowStart));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RejectsBadProtocolVersion()
    {
        var result = validator.Validate(CreateEnvelope(ProtocolEventNames.WorkflowStart, protocolVersion: "1.0"));

        Assert.False(result.IsValid);
        Assert.Equal("bad_protocol_version", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsUnknownMessageName()
    {
        var result = validator.Validate(CreateEnvelope("model.stream"));

        Assert.False(result.IsValid);
        Assert.Equal("unknown_message_name", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsMissingRequiredId()
    {
        var result = validator.Validate(CreateEnvelope(ProtocolEventNames.WorkflowStart, omitWorkflowId: true));

        Assert.False(result.IsValid);
        Assert.Equal("missing_required_field", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsInvalidUuid()
    {
        var result = validator.Validate(CreateEnvelope(ProtocolEventNames.WorkflowStart, messageId: "not-a-guid"));

        Assert.False(result.IsValid);
        Assert.Equal("invalid_id", result.ErrorCode);
    }

    [Fact]
    public void Validate_RejectsMissingAckLastSeenSequence()
    {
        var result = validator.Validate(CreateEnvelope(ProtocolEventNames.EventAck, payload: new { }));

        Assert.False(result.IsValid);
        Assert.Equal("missing_required_field", result.ErrorCode);
    }

    private static ProtocolEnvelope CreateEnvelope(
        string name,
        string protocolVersion = ProtocolEventNames.ProtocolVersion,
        string? messageId = null,
        bool omitWorkflowId = false,
        object? payload = null)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = protocolVersion,
            MessageId = messageId ?? Guid.NewGuid().ToString("D"),
            WorkspaceId = Guid.NewGuid().ToString("D"),
            WorkflowId = omitWorkflowId ? null : Guid.NewGuid().ToString("D"),
            SessionId = Guid.NewGuid().ToString("D"),
            Name = name,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(payload ?? new { lastSeenSequence = 0 }, JsonOptions)
        };
    }
}
