using System.Data;
using ChromaAgentics.Backend.Observability;
using ChromaAgentics.Backend.Persistence;
using ChromaAgentics.Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChromaAgentics.Backend.Acknowledgements;

public sealed class PostgresAcknowledgementStore(
    ChromaAgenticsDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<PostgresAcknowledgementStore> logger) : IAcknowledgementStore
{
    public async Task<long> GetLastSeenSequenceAsync(
        Guid workflowId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.EventAcknowledgements
            .AsNoTracking()
            .Where(acknowledgement =>
                acknowledgement.WorkflowId == workflowId &&
                acknowledgement.SessionId == sessionId)
            .Select(acknowledgement => (long?)acknowledgement.LastSeenSequence)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
    }

    public async Task<AcknowledgementUpdateResult> UpdateLastSeenSequenceAsync(
        Guid workspaceId,
        Guid workflowId,
        Guid sessionId,
        long lastSeenSequence,
        CancellationToken cancellationToken)
    {
        using var activity = ProtocolActivitySource.Instance.StartActivity("event.ack");
        activity?.SetTag("workflow.id", workflowId.ToString("D"));
        activity?.SetTag("session.id", sessionId.ToString("D"));
        activity?.SetTag("ack.last_seen", lastSeenSequence);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        var acknowledgement = await dbContext.EventAcknowledgements
            .FirstOrDefaultAsync(
                item => item.WorkflowId == workflowId && item.SessionId == sessionId,
                cancellationToken);

        if (acknowledgement is null)
        {
            acknowledgement = new EventAcknowledgement
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                WorkflowId = workflowId,
                SessionId = sessionId,
                LastSeenSequence = lastSeenSequence,
                UpdatedAtUtc = timeProvider.GetUtcNow()
            };

            dbContext.EventAcknowledgements.Add(acknowledgement);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "event.ack.updated workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} result=created",
                workflowId,
                sessionId,
                lastSeenSequence);

            return new AcknowledgementUpdateResult(0, lastSeenSequence, true);
        }

        var previous = acknowledgement.LastSeenSequence;
        if (lastSeenSequence <= previous)
        {
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation(
                "event.ack.noop workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} result=noop",
                workflowId,
                sessionId,
                lastSeenSequence);

            return new AcknowledgementUpdateResult(previous, previous, false);
        }

        acknowledgement.LastSeenSequence = lastSeenSequence;
        acknowledgement.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "event.ack.updated workflowId={WorkflowId} sessionId={SessionId} sequence={Sequence} result=updated",
            workflowId,
            sessionId,
            lastSeenSequence);

        return new AcknowledgementUpdateResult(previous, lastSeenSequence, true);
    }
}
