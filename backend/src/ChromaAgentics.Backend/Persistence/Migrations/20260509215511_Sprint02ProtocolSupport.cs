using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChromaAgentics.Backend.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint02ProtocolSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Mode = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: true),
                    NextSequence = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowExecutions", x => x.Id);
                    table.CheckConstraint("CK_WorkflowExecutions_Status", "\"Status\" in ('created', 'running', 'cancelled', 'completed', 'failed')");
                    table.ForeignKey(
                        name: "FK_WorkflowExecutions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastConnectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowSessions_WorkflowExecutions_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "WorkflowExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkflowSessions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventAcknowledgements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventAcknowledgements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventAcknowledgements_WorkflowExecutions_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "WorkflowExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventAcknowledgements_WorkflowSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "WorkflowSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventAcknowledgements_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ProtocolVersion = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "text", nullable: true),
                    PayloadHash = table.Column<string>(type: "text", nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionEvents_WorkflowExecutions_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "WorkflowExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutionEvents_WorkflowSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "WorkflowSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutionEvents_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventAcknowledgements_SessionId",
                table: "EventAcknowledgements",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventAcknowledgements_UpdatedAtUtc",
                table: "EventAcknowledgements",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EventAcknowledgements_WorkflowId_SessionId",
                table: "EventAcknowledgements",
                columns: new[] { "WorkflowId", "SessionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventAcknowledgements_WorkspaceId",
                table: "EventAcknowledgements",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_CreatedAtUtc",
                table: "ExecutionEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_IdempotencyKey",
                table: "ExecutionEvents",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_SessionId",
                table: "ExecutionEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_WorkflowId_MessageId",
                table: "ExecutionEvents",
                columns: new[] { "WorkflowId", "MessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_WorkflowId_Name_IdempotencyKey",
                table: "ExecutionEvents",
                columns: new[] { "WorkflowId", "Name", "IdempotencyKey" },
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_WorkflowId_Sequence",
                table: "ExecutionEvents",
                columns: new[] { "WorkflowId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionEvents_WorkspaceId",
                table: "ExecutionEvents",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_CreatedAtUtc",
                table: "WorkflowExecutions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowExecutions_WorkspaceId",
                table: "WorkflowExecutions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSessions_CreatedAtUtc",
                table: "WorkflowSessions",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSessions_Id",
                table: "WorkflowSessions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSessions_WorkflowId",
                table: "WorkflowSessions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSessions_WorkflowId_Id",
                table: "WorkflowSessions",
                columns: new[] { "WorkflowId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowSessions_WorkspaceId",
                table: "WorkflowSessions",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_CreatedAtUtc",
                table: "Workspaces",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventAcknowledgements");

            migrationBuilder.DropTable(
                name: "ExecutionEvents");

            migrationBuilder.DropTable(
                name: "WorkflowSessions");

            migrationBuilder.DropTable(
                name: "WorkflowExecutions");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
