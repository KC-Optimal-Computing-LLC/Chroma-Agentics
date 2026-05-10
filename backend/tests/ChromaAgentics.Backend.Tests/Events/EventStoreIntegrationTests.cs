using ChromaAgentics.Backend.Events;
using ChromaAgentics.Backend.Persistence.Entities;
using ChromaAgentics.Backend.Protocol;
using ChromaAgentics.Backend.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChromaAgentics.Backend.Tests.Events;

[Collection(PostgresCollection.Name)]
public sealed class EventStoreIntegrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_AppliesProtocolSupportSchema()
    {
        await postgres.ResetDatabaseAsync();

        await using var dbContext = postgres.CreateContext();

        Assert.True(await dbContext.Database.CanConnectAsync());
        Assert.Equal([], await dbContext.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task AppendEvent_PersistsRowAndIncrementsNextSequence()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var ids = await SeedWorkflowAsync(dbContext);
        var store = CreateStore(dbContext);

        var persisted = await store.AppendEventAsync(CreateAppendRequest(ids), CancellationToken.None);

        Assert.Equal(1, persisted.Sequence);
        Assert.Equal(2, await dbContext.WorkflowExecutions
            .Where(item => item.Id == ids.WorkflowId)
            .Select(item => item.NextSequence)
            .SingleAsync());
        Assert.Equal(1, await dbContext.ExecutionEvents.CountAsync());
    }

    [Fact]
    public async Task UniqueWorkflowSequence_IsEnforced()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var ids = await SeedWorkflowAsync(dbContext);

        dbContext.ExecutionEvents.Add(CreateExecutionEvent(ids, 1, Guid.NewGuid()));
        dbContext.ExecutionEvents.Add(CreateExecutionEvent(ids, 1, Guid.NewGuid()));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task ReplayQuery_ReturnsEventsAfterSequenceInOrder()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var ids = await SeedWorkflowAsync(dbContext);
        var store = CreateStore(dbContext);

        await store.AppendEventAsync(CreateAppendRequest(ids, name: "first"), CancellationToken.None);
        await store.AppendEventAsync(CreateAppendRequest(ids, name: "second"), CancellationToken.None);
        await store.AppendEventAsync(CreateAppendRequest(ids, name: "third"), CancellationToken.None);

        var replay = await store.GetEventsAfterSequenceAsync(ids.WorkflowId, 1, CancellationToken.None);

        Assert.Equal(new[] { 2L, 3L }, replay.Select(item => item.Sequence));
        Assert.Equal(new[] { "second", "third" }, replay.Select(item => item.Name));
    }

    public static async Task<SeedIds> SeedWorkflowAsync(
        ChromaAgentics.Backend.Persistence.ChromaAgenticsDbContext dbContext)
    {
        var ids = new SeedIds(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        dbContext.Workspaces.Add(new Workspace
        {
            Id = ids.WorkspaceId,
            CreatedAtUtc = now
        });
        dbContext.WorkflowExecutions.Add(new WorkflowExecution
        {
            Id = ids.WorkflowId,
            WorkspaceId = ids.WorkspaceId,
            Status = WorkflowExecution.StatusRunning,
            NextSequence = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.WorkflowSessions.Add(new WorkflowSession
        {
            Id = ids.SessionId,
            WorkspaceId = ids.WorkspaceId,
            WorkflowId = ids.WorkflowId,
            CreatedAtUtc = now,
            LastConnectedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        return ids;
    }

    public static PostgresEventStore CreateStore(
        ChromaAgentics.Backend.Persistence.ChromaAgenticsDbContext dbContext)
    {
        return new PostgresEventStore(dbContext, TimeProvider.System, NullLogger<PostgresEventStore>.Instance);
    }

    public static AppendEventRequest CreateAppendRequest(
        SeedIds ids,
        string name = ProtocolEventNames.WorkflowStatus,
        string? idempotencyKey = null,
        string? payloadHash = null)
    {
        return new AppendEventRequest(
            ids.WorkspaceId,
            ids.WorkflowId,
            ids.SessionId,
            name,
            ProtocolEventNames.ProtocolVersion,
            Guid.NewGuid(),
            null,
            null,
            idempotencyKey,
            payloadHash,
            """{"status":"running"}""");
    }

    private static ExecutionEvent CreateExecutionEvent(SeedIds ids, long sequence, Guid messageId)
    {
        return new ExecutionEvent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = ids.WorkspaceId,
            WorkflowId = ids.WorkflowId,
            SessionId = ids.SessionId,
            Sequence = sequence,
            Name = ProtocolEventNames.WorkflowStatus,
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = messageId,
            PayloadJson = """{"status":"running"}""",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}

public sealed record SeedIds(Guid WorkspaceId, Guid WorkflowId, Guid SessionId);
