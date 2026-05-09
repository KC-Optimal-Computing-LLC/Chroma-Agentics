using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChromaAgentics.Backend.Contracts;
using ChromaAgentics.Backend.Streaming;
using Microsoft.AspNetCore.TestHost;

namespace ChromaAgentics.Backend.Tests;

public sealed class EventStreamTests
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
    public async Task EventsEndpoint_ValidTokenReceivesStructuredStatusEvent()
    {
        using var factory = new TestBackendFactory();
        var webSocketClient = factory.Server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request =>
        {
            request.Headers[DevTokenAuth.HeaderName] = TestBackendFactory.ValidToken;
        };

        using var socket = await webSocketClient.ConnectAsync(new Uri("ws://localhost/ws/events"), CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, cancellation.Token);
        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
        var envelope = JsonSerializer.Deserialize<ProtocolEnvelope<JsonElement>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Event envelope could not be deserialized.");

        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        Assert.True(result.EndOfMessage);
        Assert.Equal("0.1", envelope.ProtocolVersion);
        Assert.False(string.IsNullOrWhiteSpace(envelope.MessageId));
        Assert.Equal(1, envelope.Sequence);
        Assert.Equal(ProtocolEventNames.WorkflowStatus, envelope.Name);
        Assert.False(string.IsNullOrWhiteSpace(envelope.SessionId));
        Assert.False(string.IsNullOrWhiteSpace(envelope.WorkflowId));
        Assert.Equal("connected", envelope.Payload.GetProperty("status").GetString());

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", cancellation.Token);
    }
}
