using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ChromaAgentics.Backend.Configuration;
using ChromaAgentics.Backend.Health;
using ChromaAgentics.Backend.Protocol;

namespace ChromaAgentics.Backend.Streaming;

public static class EventStreamEndpoint
{
    private const int ReceiveBufferSize = 4096;
    private const int MaxMessageBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task HandleAsync(
        HttpContext context,
        BackendOptions backendOptions,
        TimeProvider timeProvider,
        IProtocolMessageValidator validator,
        ProtocolErrorFactory errorFactory,
        IWorkflowProtocolService workflowProtocolService,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ChromaAgentics.Backend.Streaming.EventStreamEndpoint");

        if (!DevTokenAuth.IsAuthorized(context, backendOptions))
        {
            logger.LogWarning("websocket.connection.rejected result=unauthorized errorCode=unauthorized");
            await JsonResponse.WriteAsync(
                context,
                new
                {
                    code = "unauthorized",
                    message = "A valid development token is required.",
                    retryable = false
                },
                StatusCodes.Status401Unauthorized,
                context.RequestAborted);
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            logger.LogWarning("websocket.connection.rejected result=bad_request errorCode=websocket_required");
            await JsonResponse.WriteAsync(
                context,
                new
                {
                    code = "websocket_required",
                    message = "This endpoint requires a WebSocket upgrade request.",
                    retryable = false
                },
                StatusCodes.Status400BadRequest,
                context.RequestAborted);
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("websocket.connection.accepted result=ok");

        await SendEnvelopeAsync(
            socket,
            ProtocolEnvelopeFactory.CreateNonDurable(
                ProtocolEventNames.ConnectionReady,
                null,
                null,
                null,
                null,
                new
                {
                    status = "ready",
                    protocolVersion = ProtocolEventNames.ProtocolVersion
                },
                timeProvider),
            context.RequestAborted);

        await ReceiveAndDispatchAsync(
            socket,
            validator,
            errorFactory,
            workflowProtocolService,
            logger,
            context.RequestAborted);
    }

    private static async Task ReceiveAndDispatchAsync(
        WebSocket socket,
        IProtocolMessageValidator validator,
        ProtocolErrorFactory errorFactory,
        IWorkflowProtocolService workflowProtocolService,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            string json;
            try
            {
                var received = await ReceiveTextMessageAsync(socket, buffer, cancellationToken);
                if (received is null)
                {
                    return;
                }

                json = received;
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
            catch (InvalidOperationException)
            {
                await SendEnvelopeAsync(
                    socket,
                    errorFactory.Create("invalid_json", "The WebSocket message must be valid UTF-8 JSON."),
                    cancellationToken);
                continue;
            }

            ProtocolEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ProtocolEnvelope>(json, JsonOptions);
            }
            catch (JsonException)
            {
                logger.LogWarning("protocol.message.rejected result=invalid_json errorCode=invalid_json");
                await SendEnvelopeAsync(
                    socket,
                    errorFactory.Create("invalid_json", "The WebSocket message must be a valid protocol envelope."),
                    cancellationToken);
                continue;
            }

            if (envelope is null)
            {
                logger.LogWarning("protocol.message.rejected result=invalid_json errorCode=invalid_json");
                await SendEnvelopeAsync(
                    socket,
                    errorFactory.Create("invalid_json", "The WebSocket message must be a valid protocol envelope."),
                    cancellationToken);
                continue;
            }

            logger.LogInformation(
                "protocol.message.received workflowId={WorkflowId} sessionId={SessionId} name={MessageName} correlationId={CorrelationId}",
                envelope.WorkflowId,
                envelope.SessionId,
                envelope.Name,
                envelope.CorrelationId);

            var validation = validator.Validate(envelope);
            if (!validation.IsValid)
            {
                logger.LogWarning(
                    "protocol.message.rejected workflowId={WorkflowId} sessionId={SessionId} name={MessageName} correlationId={CorrelationId} errorCode={ErrorCode}",
                    envelope.WorkflowId,
                    envelope.SessionId,
                    envelope.Name,
                    envelope.CorrelationId,
                    validation.ErrorCode);

                await SendEnvelopeAsync(socket, errorFactory.FromValidation(validation, envelope), cancellationToken);
                continue;
            }

            WorkflowProtocolResult result;
            try
            {
                result = envelope.Name switch
                {
                    ProtocolEventNames.WorkflowStart => await workflowProtocolService.StartWorkflowAsync(
                        envelope,
                        cancellationToken),
                    ProtocolEventNames.SessionResume => await workflowProtocolService.ResumeSessionAsync(
                        envelope,
                        cancellationToken),
                    ProtocolEventNames.EventAck => await workflowProtocolService.AcknowledgeEventsAsync(
                        envelope,
                        cancellationToken),
                    _ => new WorkflowProtocolResult(
                    [
                        errorFactory.Create(
                            "unknown_message_name",
                            "The message name is not implemented.",
                            envelope)
                    ])
                };
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "protocol.message.rejected workflowId={WorkflowId} sessionId={SessionId} name={MessageName} correlationId={CorrelationId} errorCode=internal_error",
                    envelope.WorkflowId,
                    envelope.SessionId,
                    envelope.Name,
                    envelope.CorrelationId);

                result = new WorkflowProtocolResult(
                [
                    errorFactory.Create(
                        "internal_error",
                        "The protocol message could not be processed.",
                        envelope)
                ]);
            }

            foreach (var response in result.Envelopes)
            {
                await SendEnvelopeAsync(socket, response, cancellationToken);
            }
        }
    }

    private static async Task<string?> ReceiveTextMessageAsync(
        WebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await socket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Event stream closed.",
                    cancellationToken);
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new InvalidOperationException("Only text WebSocket messages are supported.");
            }

            stream.Write(buffer, 0, result.Count);
            if (stream.Length > MaxMessageBytes)
            {
                throw new InvalidOperationException("WebSocket protocol message is too large.");
            }

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private static async Task SendEnvelopeAsync(
        WebSocket socket,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }
}
