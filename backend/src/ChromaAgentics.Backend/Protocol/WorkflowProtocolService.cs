using System.Data;
using System.Text.Json;
using ChromaAgentics.Backend.Acknowledgements;
using ChromaAgentics.Backend.Events;
using ChromaAgentics.Backend.Observability;
using ChromaAgentics.Backend.Persistence;
using ChromaAgentics.Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChromaAgentics.Backend.Protocol;

public sealed class WorkflowProtocolService(
    ChromaAgenticsDbContext dbContext,
    IEventStore eventStore,
    IAcknowledgementStore acknowledgementStore,
    ProtocolErrorFactory errorFactory,
    TimeProvider timeProvider,
    IWorkflowStartFailureInjector workflowStartFailureInjector,
    ILogger<WorkflowProtocolService> logger) : IWorkflowProtocolService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowProtocolResult> StartWorkflowAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var activity = ProtocolActivitySource.Instance.StartActivity("workflow.start");
        var ids = ParsedIds.From(envelope);
        activity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
        activity?.SetTag("session.id", ids.SessionId.ToString("D"));

        var payloadHash = CanonicalJsonHasher.ComputeSha256(envelope.Payload);

        if (!string.IsNullOrWhiteSpace(envelope.IdempotencyKey))
        {
            var existing = await eventStore.GetEventByIdempotencyKeyAsync(
                ids.WorkflowId,
                ProtocolEventNames.WorkflowStarted,
                envelope.IdempotencyKey,
                cancellationToken);

            if (existing is not null)
            {
                if (!string.Equals(existing.PayloadHash, payloadHash, StringComparison.Ordinal))
                {
                    logger.LogWarning(
                        "protocol.message.rejected workflowId={WorkflowId} sessionId={SessionId} name={MessageName} correlationId={CorrelationId} errorCode=idempotency_conflict",
                        ids.WorkflowId,
                        ids.SessionId,
                        envelope.Name,
                        ids.CorrelationId);

                    return Error(
                        errorFactory.Create(
                            "idempotency_conflict",
                            "The idempotency key was already used with a different payload.",
                            envelope));
                }

                var replay = await dbContext.ExecutionEvents
                    .AsNoTracking()
                    .Where(executionEvent =>
                        executionEvent.WorkflowId == ids.WorkflowId &&
                        executionEvent.IdempotencyKey == envelope.IdempotencyKey &&
                        (executionEvent.Name == ProtocolEventNames.WorkflowStarted ||
                         executionEvent.Name == ProtocolEventNames.WorkflowStatus))
                    .OrderBy(executionEvent => executionEvent.Sequence)
                    .ToListAsync(cancellationToken);

                return new WorkflowProtocolResult(replay.Select(ProtocolEnvelope.FromExecutionEvent).ToList());
            }
        }

        var startedPayload = new
        {
            workflowId = ids.WorkflowId,
            sessionId = ids.SessionId,
            status = WorkflowExecution.StatusRunning,
            title = GetStringPayloadProperty(envelope.Payload, "title"),
            mode = GetStringPayloadProperty(envelope.Payload, "mode"),
            source = GetStringPayloadProperty(envelope.Payload, "source")
        };

        var statusPayload = new
        {
            status = WorkflowExecution.StatusRunning,
            detail = "Workflow shell started."
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        ExecutionEvent started;
        ExecutionEvent status;
        try
        {
            var shell = await EnsureWorkflowShellForStartAsync(envelope, ids, cancellationToken);
            if (shell.Error is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Error(errorFactory.Create(shell.Error.Value.Code, shell.Error.Value.Message, envelope));
            }

            var workflow = shell.Workflow
                ?? throw new InvalidOperationException("Workflow shell was not available for start.");

            var now = timeProvider.GetUtcNow();
            var startedSequence = workflow.NextSequence;
            var statusSequence = checked(startedSequence + 1);
            workflow.NextSequence = checked(workflow.NextSequence + 2);
            workflow.UpdatedAtUtc = now;

            started = CreateExecutionEvent(
                ids,
                ProtocolEventNames.WorkflowStarted,
                startedSequence,
                ids.CorrelationId,
                ids.MessageId,
                envelope.IdempotencyKey,
                payloadHash,
                JsonSerializer.Serialize(startedPayload, JsonOptions));

            using (var appendActivity = ProtocolActivitySource.Instance.StartActivity("event.append"))
            {
                appendActivity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
                appendActivity?.SetTag("event.name", started.Name);
                appendActivity?.SetTag("event.sequence", started.Sequence);
                dbContext.ExecutionEvents.Add(started);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await workflowStartFailureInjector.AfterWorkflowStartedPersistedAsync(cancellationToken);

            status = CreateExecutionEvent(
                ids,
                ProtocolEventNames.WorkflowStatus,
                statusSequence,
                ids.CorrelationId,
                ids.MessageId,
                envelope.IdempotencyKey,
                payloadHash,
                JsonSerializer.Serialize(statusPayload, JsonOptions));

            using (var appendActivity = ProtocolActivitySource.Instance.StartActivity("event.append"))
            {
                appendActivity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
                appendActivity?.SetTag("event.name", status.Name);
                appendActivity?.SetTag("event.sequence", status.Sequence);
                dbContext.ExecutionEvents.Add(status);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        LogAppendedEvent(started);
        LogAppendedEvent(status);

        logger.LogInformation(
            "workflow.started workflowId={WorkflowId} sessionId={SessionId} correlationId={CorrelationId} result=ok",
            ids.WorkflowId,
            ids.SessionId,
            ids.CorrelationId);

        return new WorkflowProtocolResult(
        [
            ProtocolEnvelope.FromExecutionEvent(started),
            ProtocolEnvelope.FromExecutionEvent(status)
        ]);
    }

    public async Task<WorkflowProtocolResult> ResumeSessionAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var activity = ProtocolActivitySource.Instance.StartActivity("session.resume");
        var ids = ParsedIds.From(envelope);
        var lastSeenSequence = envelope.Payload.GetProperty("lastSeenSequence").GetInt64();
        activity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
        activity?.SetTag("session.id", ids.SessionId.ToString("D"));
        activity?.SetTag("resume.last_seen", lastSeenSequence);

        var validation = await ValidateExistingWorkflowSessionAsync(ids, cancellationToken);
        if (validation is not null)
        {
            return Error(errorFactory.Create(validation.Value.Code, validation.Value.Message, envelope));
        }

        var maxSequence = await eventStore.GetMaxSequenceAsync(ids.WorkflowId, cancellationToken);
        if (lastSeenSequence > maxSequence)
        {
            return Error(errorFactory.Create(
                "future_sequence",
                "lastSeenSequence is ahead of the latest persisted workflow event.",
                envelope));
        }

        if (lastSeenSequence == maxSequence)
        {
            return new WorkflowProtocolResult(
            [
                ProtocolEnvelopeFactory.CreateNonDurable(
                    ProtocolEventNames.WorkflowStatus,
                    ids.WorkspaceId,
                    ids.WorkflowId,
                    ids.SessionId,
                    ids.CorrelationId,
                    new
                    {
                        status = "resume.current",
                        detail = "No missed events to replay.",
                        latestSequence = maxSequence
                    },
                    timeProvider)
            ]);
        }

        var events = await eventStore.GetEventsAfterSequenceAsync(
            ids.WorkflowId,
            lastSeenSequence,
            cancellationToken);

        foreach (var executionEvent in events)
        {
            using var replayActivity = ProtocolActivitySource.Instance.StartActivity("event.replay");
            replayActivity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
            replayActivity?.SetTag("event.sequence", executionEvent.Sequence);
            replayActivity?.SetTag("event.name", executionEvent.Name);

            logger.LogInformation(
                "event.replayed workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} name={MessageName} correlationId={CorrelationId} result=ok",
                ids.WorkflowId,
                ids.SessionId,
                executionEvent.Sequence,
                executionEvent.Name,
                ids.CorrelationId);
        }

        return new WorkflowProtocolResult(events.Select(ProtocolEnvelope.FromExecutionEvent).ToList());
    }

    public async Task<WorkflowProtocolResult> AcknowledgeEventsAsync(
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        using var activity = ProtocolActivitySource.Instance.StartActivity("event.ack");
        var ids = ParsedIds.From(envelope);
        var lastSeenSequence = envelope.Payload.GetProperty("lastSeenSequence").GetInt64();
        activity?.SetTag("workflow.id", ids.WorkflowId.ToString("D"));
        activity?.SetTag("session.id", ids.SessionId.ToString("D"));
        activity?.SetTag("ack.last_seen", lastSeenSequence);

        var validation = await ValidateExistingWorkflowSessionAsync(ids, cancellationToken);
        if (validation is not null)
        {
            return Error(errorFactory.Create(validation.Value.Code, validation.Value.Message, envelope));
        }

        var maxSequence = await eventStore.GetMaxSequenceAsync(ids.WorkflowId, cancellationToken);
        if (lastSeenSequence > maxSequence)
        {
            return Error(errorFactory.Create(
                "future_ack",
                "lastSeenSequence is ahead of the latest persisted workflow event.",
                envelope));
        }

        var currentAck = await acknowledgementStore.GetLastSeenSequenceAsync(
            ids.WorkflowId,
            ids.SessionId,
            cancellationToken);

        if (lastSeenSequence <= currentAck)
        {
            logger.LogInformation(
                "event.ack.noop workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} correlationId={CorrelationId} result=noop",
                ids.WorkflowId,
                ids.SessionId,
                lastSeenSequence,
                ids.CorrelationId);

            return new WorkflowProtocolResult(
            [
                ProtocolEnvelopeFactory.CreateNonDurable(
                    ProtocolEventNames.WorkflowStatus,
                    ids.WorkspaceId,
                    ids.WorkflowId,
                    ids.SessionId,
                    ids.CorrelationId,
                    new
                    {
                        status = "ack.noop",
                        lastSeenSequence = currentAck
                    },
                    timeProvider)
            ]);
        }

        var update = await acknowledgementStore.UpdateLastSeenSequenceAsync(
            ids.WorkspaceId,
            ids.WorkflowId,
            ids.SessionId,
            lastSeenSequence,
            cancellationToken);

        return new WorkflowProtocolResult(
        [
            ProtocolEnvelopeFactory.CreateNonDurable(
                ProtocolEventNames.WorkflowStatus,
                ids.WorkspaceId,
                ids.WorkflowId,
                ids.SessionId,
                ids.CorrelationId,
                new
                {
                    status = update.Updated ? "ack.updated" : "ack.noop",
                    lastSeenSequence = update.LastSeenSequence
                },
                timeProvider)
        ]);
    }

    private async Task<(WorkflowExecution? Workflow, (string Code, string Message)? Error)> EnsureWorkflowShellForStartAsync(
        ProtocolEnvelope envelope,
        ParsedIds ids,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var workspace = await dbContext.Workspaces.FindAsync([ids.WorkspaceId], cancellationToken);
        if (workspace is null)
        {
            workspace = new Workspace
            {
                Id = ids.WorkspaceId,
                CreatedAtUtc = now
            };
            dbContext.Workspaces.Add(workspace);
        }

        var workflow = await dbContext.WorkflowExecutions
            .FromSqlInterpolated(
                $"SELECT * FROM \"WorkflowExecutions\" WHERE \"Id\" = {ids.WorkflowId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (workflow is null)
        {
            workflow = new WorkflowExecution
            {
                Id = ids.WorkflowId,
                WorkspaceId = ids.WorkspaceId,
                Status = WorkflowExecution.StatusRunning,
                Title = GetStringPayloadProperty(envelope.Payload, "title"),
                Mode = GetStringPayloadProperty(envelope.Payload, "mode"),
                Source = GetStringPayloadProperty(envelope.Payload, "source"),
                NextSequence = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            dbContext.WorkflowExecutions.Add(workflow);
        }
        else
        {
            if (workflow.WorkspaceId != ids.WorkspaceId)
            {
                return (null, ("workflow_not_found", "The workflow was not found."));
            }

            workflow.Status = WorkflowExecution.StatusRunning;
            workflow.UpdatedAtUtc = now;
        }

        var session = await dbContext.WorkflowSessions.FindAsync([ids.SessionId], cancellationToken);
        if (session is null)
        {
            session = new WorkflowSession
            {
                Id = ids.SessionId,
                WorkspaceId = ids.WorkspaceId,
                WorkflowId = ids.WorkflowId,
                CreatedAtUtc = now,
                LastConnectedAtUtc = now,
                ClientName = GetStringPayloadProperty(envelope.Payload, "clientName")
            };
            dbContext.WorkflowSessions.Add(session);
        }
        else
        {
            if (session.WorkspaceId != ids.WorkspaceId || session.WorkflowId != ids.WorkflowId)
            {
                return (null, ("session_not_found", "The workflow session was not found."));
            }

            session.LastConnectedAtUtc = now;
        }

        return (workflow, null);
    }

    private async Task<(string Code, string Message)?> ValidateExistingWorkflowSessionAsync(
        ParsedIds ids,
        CancellationToken cancellationToken)
    {
        var workflow = await dbContext.WorkflowExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == ids.WorkflowId, cancellationToken);

        if (workflow is null || workflow.WorkspaceId != ids.WorkspaceId)
        {
            return ("workflow_not_found", "The workflow was not found.");
        }

        if (workflow.Status == WorkflowExecution.StatusCancelled)
        {
            return ("workflow_cancelled", "The workflow has been cancelled.");
        }

        var sessionExists = await dbContext.WorkflowSessions
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.Id == ids.SessionId &&
                    item.WorkflowId == ids.WorkflowId &&
                    item.WorkspaceId == ids.WorkspaceId,
                cancellationToken);

        return sessionExists ? null : ("session_not_found", "The workflow session was not found.");
    }

    private static WorkflowProtocolResult Error(ProtocolEnvelope envelope)
    {
        return new WorkflowProtocolResult([envelope]);
    }

    private ExecutionEvent CreateExecutionEvent(
        ParsedIds ids,
        string name,
        long sequence,
        Guid? correlationId,
        Guid causationMessageId,
        string? idempotencyKey,
        string? payloadHash,
        string payloadJson)
    {
        return new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ids.WorkspaceId,
            WorkflowId = ids.WorkflowId,
            SessionId = ids.SessionId,
            Sequence = sequence,
            Name = name,
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid(),
            CorrelationId = correlationId,
            CausationMessageId = causationMessageId,
            IdempotencyKey = idempotencyKey,
            PayloadHash = payloadHash,
            PayloadJson = payloadJson,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };
    }

    private void LogAppendedEvent(ExecutionEvent executionEvent)
    {
        logger.LogInformation(
            "event.appended workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} name={MessageName} correlationId={CorrelationId} result=ok",
            executionEvent.WorkflowId,
            executionEvent.SessionId,
            executionEvent.Sequence,
            executionEvent.Name,
            executionEvent.CorrelationId);
    }

    private static string? GetStringPayloadProperty(JsonElement payload, string propertyName)
    {
        return payload.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private readonly record struct ParsedIds(
        Guid MessageId,
        Guid WorkspaceId,
        Guid WorkflowId,
        Guid SessionId,
        Guid? CorrelationId)
    {
        public static ParsedIds From(ProtocolEnvelope envelope)
        {
            return new ParsedIds(
                Guid.Parse(envelope.MessageId!),
                Guid.Parse(envelope.WorkspaceId!),
                Guid.Parse(envelope.WorkflowId!),
                Guid.Parse(envelope.SessionId!),
                Guid.TryParse(envelope.CorrelationId, out var correlationId) ? correlationId : null);
        }
    }
}
