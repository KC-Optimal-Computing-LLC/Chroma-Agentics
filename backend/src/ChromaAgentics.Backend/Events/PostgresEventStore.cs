using System.Data;
using ChromaAgentics.Backend.Observability;
using ChromaAgentics.Backend.Persistence;
using ChromaAgentics.Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChromaAgentics.Backend.Events;

public sealed class PostgresEventStore(
    ChromaAgenticsDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PostgresEventStore> logger) : IEventStore
{
    public async Task<ExecutionEvent> AppendEventAsync(
        AppendEventRequest request,
        CancellationToken cancellationToken)
    {
        using var activity = ProtocolActivitySource.Instance.StartActivity("event.append");
        activity?.SetTag("workflow.id", request.WorkflowId.ToString("D"));
        activity?.SetTag("event.name", request.Name);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var workflow = await dbContext.WorkflowExecutions
            .FromSqlInterpolated(
                $"SELECT * FROM \"WorkflowExecutions\" WHERE \"Id\" = {request.WorkflowId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (workflow is null)
        {
            throw new InvalidOperationException("Workflow execution was not found for event append.");
        }

        var sequence = workflow.NextSequence;
        workflow.NextSequence++;
        workflow.UpdatedAtUtc = timeProvider.GetUtcNow();

        var executionEvent = new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            WorkflowId = request.WorkflowId,
            SessionId = request.SessionId,
            Sequence = sequence,
            Name = request.Name,
            ProtocolVersion = request.ProtocolVersion,
            MessageId = request.MessageId,
            CorrelationId = request.CorrelationId,
            CausationMessageId = request.CausationMessageId,
            IdempotencyKey = request.IdempotencyKey,
            PayloadHash = request.PayloadHash,
            PayloadJson = request.PayloadJson,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        dbContext.ExecutionEvents.Add(executionEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "event.appended workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} name={MessageName} correlationId={CorrelationId} result=ok",
            request.WorkflowId,
            request.SessionId,
            executionEvent.Sequence,
            request.Name,
            request.CorrelationId);

        activity?.SetTag("event.sequence", executionEvent.Sequence);
        return executionEvent;
    }

    public async Task<IReadOnlyList<ExecutionEvent>> GetEventsAfterSequenceAsync(
        Guid workflowId,
        long lastSeenSequence,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExecutionEvents
            .AsNoTracking()
            .Where(executionEvent =>
                executionEvent.WorkflowId == workflowId &&
                executionEvent.Sequence > lastSeenSequence)
            .OrderBy(executionEvent => executionEvent.Sequence)
            .ToListAsync(cancellationToken);
    }

    public async Task<long> GetMaxSequenceAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        return await dbContext.ExecutionEvents
            .AsNoTracking()
            .Where(executionEvent => executionEvent.WorkflowId == workflowId)
            .MaxAsync(executionEvent => (long?)executionEvent.Sequence, cancellationToken) ?? 0;
    }

    public async Task<ExecutionEvent?> GetEventByIdempotencyKeyAsync(
        Guid workflowId,
        string name,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExecutionEvents
            .AsNoTracking()
            .Where(executionEvent =>
                executionEvent.WorkflowId == workflowId &&
                executionEvent.Name == name &&
                executionEvent.IdempotencyKey == idempotencyKey)
            .OrderBy(executionEvent => executionEvent.Sequence)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ExecutionEvent?> GetEventByMessageIdAsync(
        Guid workflowId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ExecutionEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                executionEvent =>
                    executionEvent.WorkflowId == workflowId &&
                    executionEvent.MessageId == messageId,
                cancellationToken);
    }
}
