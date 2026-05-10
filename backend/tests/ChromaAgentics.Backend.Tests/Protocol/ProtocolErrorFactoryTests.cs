using System.Text.Json;
using ChromaAgentics.Backend.Protocol;

namespace ChromaAgentics.Backend.Tests.Protocol;

public sealed class ProtocolErrorFactoryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Create_ReturnsSafeErrorEnvelope()
    {
        var request = new ProtocolEnvelope
        {
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = Guid.NewGuid().ToString("D"),
            WorkflowId = Guid.NewGuid().ToString("D"),
            SessionId = Guid.NewGuid().ToString("D"),
            Name = ProtocolEventNames.WorkflowStart,
            CorrelationId = Guid.NewGuid().ToString("D"),
            IdempotencyKey = "client-idempotency-key",
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(new { prompt = "do not echo payloads" }, JsonOptions)
        };
        var factory = new ProtocolErrorFactory(TimeProvider.System);

        var error = factory.Create("future_ack", "lastSeenSequence is too high.", request);

        Assert.Equal(ProtocolEventNames.ProtocolVersion, error.ProtocolVersion);
        Assert.Equal(ProtocolEventNames.Error, error.Name);
        Assert.Null(error.Sequence);
        Assert.Null(error.IdempotencyKey);
        Assert.Equal(request.WorkspaceId, error.WorkspaceId);
        Assert.Equal("future_ack", error.Payload.GetProperty("code").GetString());
        Assert.False(error.Payload.GetProperty("retryable").GetBoolean());
        Assert.DoesNotContain("do not echo payloads", JsonSerializer.Serialize(error, JsonOptions));
    }
}
