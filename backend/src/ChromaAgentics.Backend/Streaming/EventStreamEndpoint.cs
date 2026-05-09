using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChromaAgentics.Backend.Configuration;
using ChromaAgentics.Backend.Contracts;
using ChromaAgentics.Backend.Health;

namespace ChromaAgentics.Backend.Streaming;

public static class EventStreamEndpoint
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(
        HttpContext context,
        BackendOptions backendOptions,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ChromaAgentics.Backend.Streaming.EventStreamEndpoint");

        if (!DevTokenAuth.IsAuthorized(context, backendOptions))
        {
            await JsonResponse.WriteAsync(
                context,
                new ErrorPayload
                {
                    Code = "unauthorized",
                    Message = "A valid development token is required."
                },
                StatusCodes.Status401Unauthorized,
                context.RequestAborted);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            await JsonResponse.WriteAsync(
                context,
                new ErrorPayload
                {
                    Code = "websocket_required",
                    Message = "This endpoint requires a WebSocket upgrade request."
                },
                StatusCodes.Status400BadRequest,
                context.RequestAborted);
            return;
        }

        string? sessionError = null;
        string? workflowError = null;
        if (!TryGetContextId(context, "sessionId", out var sessionId, out sessionError) ||
            !TryGetContextId(context, "workflowId", out var workflowId, out workflowError))
        {
            await JsonResponse.WriteAsync(
                context,
                new ErrorPayload
                {
                    Code = "invalid_context",
                    Message = sessionError ?? workflowError ?? "Invalid WebSocket context."
                },
                StatusCodes.Status400BadRequest,
                context.RequestAborted);
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();

        var envelope = new ProtocolEnvelope<WorkflowStatusPayload>
        {
            MessageId = Guid.NewGuid().ToString("D"),
            WorkflowId = workflowId,
            SessionId = sessionId,
            Sequence = 1,
            Name = ProtocolEventNames.WorkflowStatus,
            Timestamp = timeProvider.GetUtcNow().UtcDateTime,
            Payload = new WorkflowStatusPayload
            {
                Status = "connected",
                Detail = "Event stream connected."
            }
        };

        await SendEnvelopeAsync(socket, envelope, context.RequestAborted);
        await ReceiveUntilCloseAsync(socket, logger, context.RequestAborted);
    }

    private static async Task SendEnvelopeAsync<TPayload>(
        WebSocket socket,
        ProtocolEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task ReceiveUntilCloseAsync(
        WebSocket socket,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1024];

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (WebSocketException exception)
            {
                logger.LogDebug(exception, "WebSocket receive loop ended unexpectedly.");
                return;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Event stream closed.",
                    cancellationToken);
                return;
            }
        }
    }

    private static bool TryGetContextId(
        HttpContext context,
        string queryName,
        out string value,
        out string? error)
    {
        var provided = context.Request.Query[queryName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided))
        {
            value = Guid.NewGuid().ToString("D");
            error = null;
            return true;
        }

        if (Guid.TryParse(provided, out var parsed))
        {
            value = parsed.ToString("D");
            error = null;
            return true;
        }

        value = string.Empty;
        error = $"{queryName} must be a UUID when provided.";
        return false;
    }
}
