using System.Text.Json;
using ChromaAgentics.Backend.Acknowledgements;
using ChromaAgentics.Backend.Events;
using ChromaAgentics.Backend.Protocol;
using ChromaAgentics.Backend.Tests.Events;
using ChromaAgentics.Backend.Tests.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChromaAgentics.Backend.Tests.Protocol;

[Collection(PostgresCollection.Name)]
public sealed class WorkflowProtocolServiceIntegrationTests(PostgresFixture postgres)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task DuplicateIdempotencyKey_DoesNotCreateDuplicateEvents()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var service = CreateService(dbContext);
        var envelope = CreateWorkflowStartEnvelope(idempotencyKey: "duplicate-key");

        var first = await service.StartWorkflowAsync(envelope, CancellationToken.None);
        var second = await service.StartWorkflowAsync(envelope, CancellationToken.None);

        Assert.Equal(2, first.Envelopes.Count);
        Assert.Equal(new long?[] { 1, 2 }, second.Envelopes.Select(item => item.Sequence));
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync());
    }

    [Fact]
    public async Task WorkflowStart_CommitsShellAndBothEventsAtomically()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var service = CreateService(dbContext);
        var envelope = CreateWorkflowStartEnvelope(idempotencyKey: "atomic-start");
        var workflowId = Guid.Parse(envelope.WorkflowId!);

        var result = await service.StartWorkflowAsync(envelope, CancellationToken.None);

        Assert.Equal(new long?[] { 1, 2 }, result.Envelopes.Select(item => item.Sequence));
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
        Assert.Equal(3, await dbContext.WorkflowExecutions
            .Where(item => item.Id == workflowId)
            .Select(item => item.NextSequence)
            .SingleAsync());
        Assert.Equal(1, await dbContext.Workspaces.CountAsync());
        Assert.Equal(1, await dbContext.WorkflowSessions.CountAsync());
    }

    [Fact]
    public async Task WorkflowStart_WhenStatusAppendFails_RollsBackShellAndStartedEvent()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var service = CreateService(dbContext, new ThrowAfterStartedFailureInjector());
        var envelope = CreateWorkflowStartEnvelope(idempotencyKey: "atomic-rollback");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartWorkflowAsync(envelope, CancellationToken.None));

        Assert.Equal(0, await dbContext.ExecutionEvents.CountAsync());
        Assert.Equal(0, await dbContext.WorkflowExecutions.CountAsync());
        Assert.Equal(0, await dbContext.WorkflowSessions.CountAsync());
        Assert.Equal(0, await dbContext.Workspaces.CountAsync());
    }

    [Fact]
    public async Task DuplicateIdempotencyKeyWithReorderedPayload_ReturnsExistingEvents()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var service = CreateService(dbContext);
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var first = CreateWorkflowStartEnvelope(
            workflowId,
            sessionId,
            "reordered-key",
            JsonDocument.Parse(
                """{"title":"A","mode":"orchestrator","nested":{"z":2,"a":1},"items":[{"b":2,"a":1},3]}""")
                .RootElement
                .Clone());
        var second = CreateWorkflowStartEnvelope(
            workflowId,
            sessionId,
            "reordered-key",
            JsonDocument.Parse(
                """{"items":[{"a":1,"b":2},3],"nested":{"a":1,"z":2},"mode":"orchestrator","title":"A"}""")
                .RootElement
                .Clone());

        var original = await service.StartWorkflowAsync(first, CancellationToken.None);
        var retry = await service.StartWorkflowAsync(second, CancellationToken.None);

        Assert.Equal(new long?[] { 1, 2 }, original.Envelopes.Select(item => item.Sequence));
        Assert.Equal(new long?[] { 1, 2 }, retry.Envelopes.Select(item => item.Sequence));
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync(item => item.WorkflowId == workflowId));
    }

    [Fact]
    public async Task SameIdempotencyKeyWithDifferentPayload_ReturnsConflict()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var service = CreateService(dbContext);
        var workflowId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var first = CreateWorkflowStartEnvelope(workflowId, sessionId, "conflict-key", new { title = "one" });
        var second = CreateWorkflowStartEnvelope(workflowId, sessionId, "conflict-key", new { title = "two" });

        await service.StartWorkflowAsync(first, CancellationToken.None);
        var conflict = await service.StartWorkflowAsync(second, CancellationToken.None);

        Assert.Single(conflict.Envelopes);
        Assert.Equal(ProtocolEventNames.Error, conflict.Envelopes[0].Name);
        Assert.Equal("idempotency_conflict", conflict.Envelopes[0].Payload.GetProperty("code").GetString());
        Assert.Equal(2, await dbContext.ExecutionEvents.CountAsync());
    }

    [Fact]
    public async Task Acknowledgement_PersistsAndUpdatesCumulatively()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var ids = await EventStoreIntegrationTests.SeedWorkflowAsync(dbContext);
        var eventStore = EventStoreIntegrationTests.CreateStore(dbContext);
        await eventStore.AppendEventAsync(EventStoreIntegrationTests.CreateAppendRequest(ids), CancellationToken.None);
        await eventStore.AppendEventAsync(EventStoreIntegrationTests.CreateAppendRequest(ids), CancellationToken.None);
        var service = CreateService(dbContext);

        var updated = await service.AcknowledgeEventsAsync(
            CreateAckEnvelope(ids, 2),
            CancellationToken.None);
        var noop = await service.AcknowledgeEventsAsync(
            CreateAckEnvelope(ids, 1),
            CancellationToken.None);

        Assert.Equal("ack.updated", updated.Envelopes[0].Payload.GetProperty("status").GetString());
        Assert.Equal("ack.noop", noop.Envelopes[0].Payload.GetProperty("status").GetString());
        Assert.Equal(2, await dbContext.EventAcknowledgements
            .Where(item => item.WorkflowId == ids.WorkflowId && item.SessionId == ids.SessionId)
            .Select(item => item.LastSeenSequence)
            .SingleAsync());
    }

    [Fact]
    public async Task FutureAck_ReturnsError()
    {
        await postgres.ResetDatabaseAsync();
        await using var dbContext = postgres.CreateContext();
        var ids = await EventStoreIntegrationTests.SeedWorkflowAsync(dbContext);
        var eventStore = EventStoreIntegrationTests.CreateStore(dbContext);
        await eventStore.AppendEventAsync(EventStoreIntegrationTests.CreateAppendRequest(ids), CancellationToken.None);
        var service = CreateService(dbContext);

        var result = await service.AcknowledgeEventsAsync(CreateAckEnvelope(ids, 2), CancellationToken.None);

        Assert.Equal(ProtocolEventNames.Error, result.Envelopes[0].Name);
        Assert.Equal("future_ack", result.Envelopes[0].Payload.GetProperty("code").GetString());
    }

    private static WorkflowProtocolService CreateService(
        ChromaAgentics.Backend.Persistence.ChromaAgenticsDbContext dbContext,
        IWorkflowStartFailureInjector? failureInjector = null)
    {
        var eventStore = new PostgresEventStore(
            dbContext,
            TimeProvider.System,
            NullLogger<PostgresEventStore>.Instance);
        var acknowledgementStore = new PostgresAcknowledgementStore(
            dbContext,
            TimeProvider.System,
            NullLogger<PostgresAcknowledgementStore>.Instance);
        var errorFactory = new ProtocolErrorFactory(TimeProvider.System);

        return new WorkflowProtocolService(
            dbContext,
            eventStore,
            acknowledgementStore,
            errorFactory,
            TimeProvider.System,
            failureInjector ?? new NoopWorkflowStartFailureInjector(),
            NullLogger<WorkflowProtocolService>.Instance);
    }

    private static ProtocolEnvelope CreateWorkflowStartEnvelope(
        Guid? workflowId = null,
        Guid? sessionId = null,
        string? idempotencyKey = null,
        object? payload = null)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = Guid.NewGuid().ToString("D"),
            WorkflowId = (workflowId ?? Guid.NewGuid()).ToString("D"),
            SessionId = (sessionId ?? Guid.NewGuid()).ToString("D"),
            Name = ProtocolEventNames.WorkflowStart,
            Timestamp = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
            Payload = JsonSerializer.SerializeToElement(payload ?? new { title = "workflow" }, JsonOptions)
        };
    }

    private static ProtocolEnvelope CreateAckEnvelope(SeedIds ids, long lastSeenSequence)
    {
        return new ProtocolEnvelope
        {
            ProtocolVersion = ProtocolEventNames.ProtocolVersion,
            MessageId = Guid.NewGuid().ToString("D"),
            WorkspaceId = ids.WorkspaceId.ToString("D"),
            WorkflowId = ids.WorkflowId.ToString("D"),
            SessionId = ids.SessionId.ToString("D"),
            Name = ProtocolEventNames.EventAck,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = JsonSerializer.SerializeToElement(new { lastSeenSequence }, JsonOptions)
        };
    }

    private sealed class ThrowAfterStartedFailureInjector : IWorkflowStartFailureInjector
    {
        public Task AfterWorkflowStartedPersistedAsync(CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated failure between workflow.started and workflow.status.");
        }
    }
}
