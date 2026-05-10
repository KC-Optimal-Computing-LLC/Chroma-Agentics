using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChromaAgentics.Backend.Protocol;
using ChromaAgentics.Backend.Streaming;
using ChromaAgentics.Backend.Tests.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace ChromaAgentics.Backend.Tests.Streaming;

[Collection(PostgresCollection.Name)]
public sealed class EventStreamTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task EventsEndpoint_RejectsMissingToken_BeforeWebSocketUpgrade()
    {
        using var factory = new TestBackendFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/ws/events");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EventsEndpoint_RejectsInvalidToken_BeforeWebSocketUpgrade()
    {
        using var factory = new TestBackendFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/ws/events");
        request.Headers.Add(DevTokenAuth.HeaderName, "invalid-token");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EventsEndpoint_ValidTokenReceivesConnectionReady()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);

        var envelope = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(ProtocolEventNames.ProtocolVersion, envelope.ProtocolVersion);
        Assert.Equal(ProtocolEventNames.ConnectionReady, envelope.Name);
        Assert.Null(envelope.Sequence);
        Assert.Equal("ready", envelope.Payload.GetProperty("status").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", CancellationToken.None);
    }

    [Fact]
    public async Task EventsEndpoint_InvalidJson_ReturnsErrorEnvelope()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        await SendRawAsync(socket, "{");
        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(ProtocolEventNames.Error, error.Name);
        Assert.Equal("invalid_json", error.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_BadProtocolVersion_ReturnsErrorEnvelope()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        var message = CreateEnvelope(
            ProtocolEventNames.WorkflowStart,
            protocolVersion: "1.0",
            payload: new { title = "bad version" });

        await SendEnvelopeAsync(socket, message);
        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("bad_protocol_version", error.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_UnknownMessageName_ReturnsErrorEnvelope()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        await SendEnvelopeAsync(socket, CreateEnvelope("model.stream", payload: new { }));
        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("unknown_message_name", error.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_MissingRequiredIds_ReturnsErrorEnvelope()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        var message = CreateEnvelope(ProtocolEventNames.WorkflowStart, omitWorkflowId: true, payload: new { });
        await SendEnvelopeAsync(socket, message);
        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("missing_required_field", error.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_RecoverableProtocolErrors_AreNotPersisted()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        await SendRawAsync(socket, "{");
        await AssertErrorAndNoEventsAsync(socket, "invalid_json");

        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                protocolVersion: "1.0",
                payload: new { title = "bad version" }));
        await AssertErrorAndNoEventsAsync(socket, "bad_protocol_version");

        await SendEnvelopeAsync(socket, CreateEnvelope("model.stream", payload: new { }));
        await AssertErrorAndNoEventsAsync(socket, "unknown_message_name");

        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(ProtocolEventNames.WorkflowStart, omitWorkflowId: true, payload: new { }));
        await AssertErrorAndNoEventsAsync(socket, "missing_required_field");
    }

    [Fact]
    public async Task EventsEndpoint_WorkflowStart_PersistsAndReturnsStartedEvents()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);

        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                workflowId: workflowId,
                sessionId: sessionId,
                idempotencyKey: "start-key",
                payload: new
                {
                    title = "Smoke test workflow",
                    mode = "orchestrator",
                    source = "manual-smoke-test"
                }));

        var started = await ReceiveEnvelopeAsync(socket);
        var status = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(ProtocolEventNames.WorkflowStarted, started.Name);
        Assert.Equal(1, started.Sequence);
        Assert.Equal(ProtocolEventNames.WorkflowStatus, status.Name);
        Assert.Equal(2, status.Sequence);

        await using var dbContext = postgres.CreateContext();
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
    }

    [Fact]
    public async Task EventsEndpoint_ResumeFromZero_ReplaysAllEventsInOrder()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "resume-zero");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.SessionResume,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 0 }));

        var first = await ReceiveEnvelopeAsync(socket);
        var second = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
    }

    [Fact]
    public async Task EventsEndpoint_ResumeFromMiddle_ReplaysOnlyMissedEvents()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "resume-middle");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.SessionResume,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 1 }));

        var missed = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(2, missed.Sequence);
        Assert.Equal(ProtocolEventNames.WorkflowStatus, missed.Name);
    }

    [Fact]
    public async Task EventsEndpoint_ResumeFromLatest_ReturnsNonDurableStatus()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "resume-latest");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.SessionResume,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 2 }));

        var status = await ReceiveEnvelopeAsync(socket);

        Assert.Null(status.Sequence);
        Assert.Equal("resume.current", status.Payload.GetProperty("status").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_FutureResume_ReturnsErrorAndDoesNotPersistEvent()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "future-resume");

        await using var beforeContext = postgres.CreateContext();
        var countBefore = await beforeContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId);

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.SessionResume,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 999 }));

        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(ProtocolEventNames.Error, error.Name);
        Assert.Null(error.Sequence);
        Assert.Equal("future_sequence", error.Payload.GetProperty("code").GetString());

        await using var afterContext = postgres.CreateContext();
        Assert.Equal(countBefore, await afterContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
    }

    [Fact]
    public async Task EventsEndpoint_DuplicateIdempotencyKey_ReturnsExistingEvents()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var first = await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "same-key");
        var second = await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "same-key");

        Assert.Equal(new long?[] { 1, 2 }, first.Select(item => item.Sequence));
        Assert.Equal(new long?[] { 1, 2 }, second.Select(item => item.Sequence));

        await using var dbContext = postgres.CreateContext();
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
    }

    [Fact]
    public async Task EventsEndpoint_DuplicateIdempotencyKeyWithReorderedPayload_ReturnsExistingEvents()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        using var firstPayload = JsonDocument.Parse(
            """{"title":"Smoke test workflow","mode":"orchestrator","nested":{"z":2,"a":1},"items":[{"b":2,"a":1},3]}""");
        using var secondPayload = JsonDocument.Parse(
            """{"items":[{"a":1,"b":2},3],"nested":{"a":1,"z":2},"mode":"orchestrator","title":"Smoke test workflow"}""");

        using var firstSocket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(firstSocket);
        await SendEnvelopeAsync(
            firstSocket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                idempotencyKey: "reordered-websocket-key",
                payload: firstPayload.RootElement.Clone()));
        var first = new[] { await ReceiveEnvelopeAsync(firstSocket), await ReceiveEnvelopeAsync(firstSocket) };

        using var secondSocket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(secondSocket);
        await SendEnvelopeAsync(
            secondSocket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                idempotencyKey: "reordered-websocket-key",
                payload: secondPayload.RootElement.Clone()));
        var second = new[] { await ReceiveEnvelopeAsync(secondSocket), await ReceiveEnvelopeAsync(secondSocket) };

        Assert.Equal(new long?[] { 1, 2 }, first.Select(item => item.Sequence));
        Assert.Equal(new long?[] { 1, 2 }, second.Select(item => item.Sequence));

        await using var dbContext = postgres.CreateContext();
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
    }

    [Fact]
    public async Task EventsEndpoint_IdempotencyConflict_ReturnsError()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "conflict-key");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                idempotencyKey: "conflict-key",
                payload: new { title = "changed" }));

        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("idempotency_conflict", error.Payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task EventsEndpoint_DuplicateOrLowerAck_ReturnsNoop()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "ack-noop");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.EventAck,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 2 }));
        _ = await ReceiveEnvelopeAsync(socket);

        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.EventAck,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 1 }));
        var noop = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("ack.noop", noop.Payload.GetProperty("status").GetString());
        Assert.Equal(2, noop.Payload.GetProperty("lastSeenSequence").GetInt64());
    }

    [Fact]
    public async Task EventsEndpoint_FutureAck_ReturnsError()
    {
        await postgres.ResetDatabaseAsync();
        using var factory = postgres.CreateBackendFactory();
        var workspaceId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await StartWorkflowAsync(factory, workspaceId, workflowId, sessionId, "future-ack");

        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.EventAck,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                payload: new { lastSeenSequence = 99 }));

        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal("future_ack", error.Payload.GetProperty("code").GetString());
    }

    private static async Task<IReadOnlyList<ProtocolEnvelope>> StartWorkflowAsync(
        TestBackendFactory factory,
        Guid workspaceId,
        Guid workflowId,
        Guid sessionId,
        string idempotencyKey)
    {
        using var socket = await ConnectAsync(factory);
        _ = await ReceiveEnvelopeAsync(socket);
        await SendEnvelopeAsync(
            socket,
            CreateEnvelope(
                ProtocolEventNames.WorkflowStart,
                workspaceId: workspaceId,
                workflowId: workflowId,
                sessionId: sessionId,
                idempotencyKey: idempotencyKey,
                payload: new
                {
                    title = "Smoke test workflow",
                    mode = "orchestrator",
                    source = "manual-smoke-test"
                }));

        return [await ReceiveEnvelopeAsync(socket), await ReceiveEnvelopeAsync(socket)];
    }

    private static async Task<WebSocket> ConnectAsync(TestBackendFactory factory)
    {
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request =>
        {
            request.Headers[DevTokenAuth.HeaderName] = TestBackendFactory.ValidToken;
        };

        return await webSocketClient.ConnectAsync(new Uri("ws://localhost/ws/events"), CancellationToken.None);
    }

    private static ProtocolEnvelope CreateEnvelope(
        string name,
        string protocolVersion = ProtocolEventNames.ProtocolVersion,
        Guid? workspaceId = null,
        Guid? workflowId = null,
        Guid? sessionId = null,
        string? idempotencyKey = null,
        bool omitWorkflowId = false,
        object? payload = null)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = protocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = (workspaceId ?? Guid.NewGuid()).ToString("D"),
            WorkflowId = omitWorkflowId ? null : workflowId?.ToString("D") ?? Guid.NewGuid().ToString("D"),
            SessionId = (sessionId ?? Guid.NewGuid()).ToString("D"),
            Sequence = null,
            Name = name,
            CorrelationId = Guid.NewGuid().ToString("D"),
            IdempotencyKey = idempotencyKey,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(payload ?? new { }, JsonOptions)
        };
    }

    private static async Task SendEnvelopeAsync(WebSocket socket, ProtocolEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await SendRawAsync(socket, json);
    }

    private static async Task SendRawAsync(WebSocket socket, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<ProtocolEnvelope> ReceiveEnvelopeAsync(WebSocket socket)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var buffer = new byte[16 * 1024];
        var result = await socket.ReceiveAsync(buffer, cancellation.Token);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);

        return JsonSerializer.Deserialize<ProtocolEnvelope>(json, JsonOptions)
            ?? throw new InvalidOperationException("Event envelope could not be deserialized.");
    }

    private async Task AssertErrorAndNoEventsAsync(WebSocket socket, string expectedCode)
    {
        var error = await ReceiveEnvelopeAsync(socket);

        Assert.Equal(ProtocolEventNames.Error, error.Name);
        Assert.Null(error.Sequence);
        Assert.Equal(expectedCode, error.Payload.GetProperty("code").GetString());

        await using var dbContext = postgres.CreateContext();
        Assert.Equal(0, await dbContext.ExecutionEvents.CountAsync());
    }
}
