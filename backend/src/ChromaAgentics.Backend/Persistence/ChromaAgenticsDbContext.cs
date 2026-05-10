using ChromaAgentics.Backend.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChromaAgentics.Backend.Persistence;

public sealed class ChromaAgenticsDbContext(DbContextOptions<ChromaAgenticsDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkflowExecution> WorkflowExecutions => Set<WorkflowExecution>();
    public DbSet<WorkflowSession> WorkflowSessions => Set<WorkflowSession>();
    public DbSet<ExecutionEvent> ExecutionEvents => Set<ExecutionEvent>();
    public DbSet<EventAcknowledgement> EventAcknowledgements => Set<EventAcknowledgement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Workspace>(entity =>
        {
            entity.ToTable("Workspaces");
            entity.HasKey(workspace => workspace.Id);
            entity.Property(workspace => workspace.Name).HasColumnType("text");
            entity.Property(workspace => workspace.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(workspace => workspace.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(workspace => workspace.CreatedAtUtc);
        });

        modelBuilder.Entity<WorkflowExecution>(entity =>
        {
            entity.ToTable("WorkflowExecutions");
            entity.HasKey(workflow => workflow.Id);
            entity.Property(workflow => workflow.Status).HasColumnType("text");
            entity.Property(workflow => workflow.Title).HasColumnType("text");
            entity.Property(workflow => workflow.Mode).HasColumnType("text");
            entity.Property(workflow => workflow.Source).HasColumnType("text");
            entity.Property(workflow => workflow.NextSequence).HasDefaultValue(1L);
            entity.Property(workflow => workflow.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(workflow => workflow.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(workflow => workflow.CancelledAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(workflow => workflow.CancellationReason).HasColumnType("text");
            entity.HasOne(workflow => workflow.Workspace)
                .WithMany()
                .HasForeignKey(workflow => workflow.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(workflow => workflow.WorkspaceId);
            entity.HasIndex(workflow => workflow.CreatedAtUtc);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_WorkflowExecutions_Status",
                "\"Status\" in ('created', 'running', 'cancelled', 'completed', 'failed')"));
        });

        modelBuilder.Entity<WorkflowSession>(entity =>
        {
            entity.ToTable("WorkflowSessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(session => session.LastConnectedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(session => session.ClosedAtUtc).HasColumnType("timestamp with time zone");
            entity.Property(session => session.ClientName).HasColumnType("text");
            entity.HasOne(session => session.Workspace)
                .WithMany()
                .HasForeignKey(session => session.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(session => session.Workflow)
                .WithMany()
                .HasForeignKey(session => session.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(session => session.WorkspaceId);
            entity.HasIndex(session => session.WorkflowId);
            entity.HasIndex(session => session.Id);
            entity.HasIndex(session => new { session.WorkflowId, session.Id }).IsUnique();
            entity.HasIndex(session => session.CreatedAtUtc);
        });

        modelBuilder.Entity<ExecutionEvent>(entity =>
        {
            entity.ToTable("ExecutionEvents");
            entity.HasKey(executionEvent => executionEvent.Id);
            entity.Property(executionEvent => executionEvent.Name).HasColumnType("text");
            entity.Property(executionEvent => executionEvent.ProtocolVersion).HasColumnType("text");
            entity.Property(executionEvent => executionEvent.IdempotencyKey).HasColumnType("text");
            entity.Property(executionEvent => executionEvent.PayloadHash).HasColumnType("text");
            entity.Property(executionEvent => executionEvent.PayloadJson).HasColumnType("jsonb");
            entity.Property(executionEvent => executionEvent.CreatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasOne(executionEvent => executionEvent.Workspace)
                .WithMany()
                .HasForeignKey(executionEvent => executionEvent.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(executionEvent => executionEvent.Workflow)
                .WithMany()
                .HasForeignKey(executionEvent => executionEvent.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(executionEvent => executionEvent.Session)
                .WithMany()
                .HasForeignKey(executionEvent => executionEvent.SessionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(executionEvent => executionEvent.WorkspaceId);
            entity.HasIndex(executionEvent => executionEvent.SessionId);
            entity.HasIndex(executionEvent => executionEvent.IdempotencyKey);
            entity.HasIndex(executionEvent => executionEvent.CreatedAtUtc);
            entity.HasIndex(executionEvent => new { executionEvent.WorkflowId, executionEvent.Sequence }).IsUnique();
            entity.HasIndex(executionEvent => new { executionEvent.WorkflowId, executionEvent.MessageId }).IsUnique();
            entity.HasIndex(executionEvent => new
                {
                    executionEvent.WorkflowId,
                    executionEvent.Name,
                    executionEvent.IdempotencyKey
                })
                .IsUnique()
                .HasFilter("\"IdempotencyKey\" IS NOT NULL");
        });

        modelBuilder.Entity<EventAcknowledgement>(entity =>
        {
            entity.ToTable("EventAcknowledgements");
            entity.HasKey(acknowledgement => acknowledgement.Id);
            entity.Property(acknowledgement => acknowledgement.UpdatedAtUtc).HasColumnType("timestamp with time zone");
            entity.HasOne(acknowledgement => acknowledgement.Workspace)
                .WithMany()
                .HasForeignKey(acknowledgement => acknowledgement.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(acknowledgement => acknowledgement.Workflow)
                .WithMany()
                .HasForeignKey(acknowledgement => acknowledgement.WorkflowId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(acknowledgement => acknowledgement.Session)
                .WithMany()
                .HasForeignKey(acknowledgement => acknowledgement.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(acknowledgement => acknowledgement.WorkspaceId);
            entity.HasIndex(acknowledgement => acknowledgement.SessionId);
            entity.HasIndex(acknowledgement => acknowledgement.UpdatedAtUtc);
            entity.HasIndex(acknowledgement => new
                {
                    acknowledgement.WorkflowId,
                    acknowledgement.SessionId
                })
                .IsUnique();
        });
    }
}
