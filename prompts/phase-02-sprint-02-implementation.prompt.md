# Implementation Prompt: Phase 2 Sprint 2 — Durable Event Store + Resume/Replay

You are working in the Chroma Agentics repository.

Implement Phase 2 Sprint 2: Durable Event Store + Resume/Replay.

This sprint extends the Sprint 1 backend foundation with PostgreSQL-backed protocol durability, event sequencing, session resume, replay, and cumulative ACK handling.

Do not expand scope beyond Sprint 2. Do not implement systems planned for Phase 3 or beyond, such as real approval execution, extension bridge UI, Ollama chat adapters, pgvector/RAG, or full Microsoft Agent Framework workflows. Cancellation support is explicitly out of scope for Sprint 2; document it only as future stretch work.

---

## Source of Truth

Use:

- `README.md`
- `ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md`
- `docs/API_CONTRACT.md`
- `docs/PHASE_02_SPRINT_01_REPORT.md`
- `agents/phase-02-sprint-02-durable-event-store.agent.md`
- existing Sprint 1 backend implementation

Preserve the architecture boundary:

- VS Code/Roo-derived extension owns UI, approvals, file edits, terminal execution, and MCP/tool execution.
- Backend owns durable protocol state, WebSocket event streaming, session resume, replay, and future orchestration support.
- Backend must not execute file edits, terminal commands, MCP actions, or approval decisions.

---

## Required First Step: Sprint 1 Gate

Before editing, inspect and verify Sprint 1.

Confirm:

```text
backend solution exists
backend builds
backend tests pass
health endpoints exist
/ws/events exists
/ws/events requires dev-token auth
.env.example exists
docker-compose.yml exists
docs/PHASE_02_SPRINT_01_REPORT.md exists

Run or verify:

dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config

If Sprint 1 is broken, stop and report blockers unless the issue is clearly non-blocking and documented.

If the specified file paths do not exist or the repository uses a different backend layout, adapt only as needed to follow the existing backend conventions.

Output a short Sprint 1 gate summary before implementation.

Required Second Step: Repo Inspection

Inspect and report:

Sprint 1 backend structure
existing health/config/WebSocket files
existing protocol envelope types
existing package versions
existing Docker/PostgreSQL setup
existing docs
existing tests
conflicts with this sprint plan

Do not assume paths. Adapt only when the repo structure requires it.

Sprint Goal

Implement the smallest durable protocol slice.

Architecture and scope:
- preserve the extension/backend boundary
- backend must not execute file edits, terminal commands, MCP actions, or approval decisions

Persistence:
- EF Core PostgreSQL persistence
- minimal protocol-support schema
- transaction-safe event append
- monotonic per-workflow sequence numbers

Protocol behavior:
- workflow.start handling
- session.resume handling
- event.ack handling
- ordered replay of events where sequence > lastSeenSequence
- idempotency handling

Observability and docs:
- safe structured error envelopes
- structured logs
- minimal ActivitySource spans
- protocol 0.2 docs
- starter JSON Schema contract artifacts
- Sprint 2 report

Use:

protocolVersion = "0.2"

Do not claim protocol 1.0.

Package Policy

Target net8.0.

Pin all new packages exactly. Preserve Sprint 1 package pins unless there is a documented compatibility reason to change them.

Add EF/PostgreSQL packages:

Microsoft.EntityFrameworkCore
Microsoft.EntityFrameworkCore.Design
Npgsql.EntityFrameworkCore.PostgreSQL

Preferred integration test package:

Testcontainers.PostgreSql

If Testcontainers is not practical, document the fallback:

Docker Compose PostgreSQL
configured local PostgreSQL

Document all final package versions in:

docs/PHASE_02_SPRINT_02_REPORT.md
Required File Targets

Create or update files under the existing backend structure. Use these targets unless repo conventions require adjustment:

backend/src/ChromaAgentics.Backend/
  Persistence/
    ChromaAgenticsDbContext.cs
    Entities/
      Workspace.cs
      WorkflowExecution.cs
      WorkflowSession.cs
      ExecutionEvent.cs
      EventAcknowledgement.cs
    Migrations/
  Events/
    IEventStore.cs
    PostgresEventStore.cs
  Acknowledgements/
    IAcknowledgementStore.cs
    PostgresAcknowledgementStore.cs
  Protocol/
    IWorkflowProtocolService.cs
    WorkflowProtocolService.cs
    IProtocolMessageValidator.cs
    ProtocolMessageValidator.cs
    ProtocolErrorFactory.cs
    ProtocolEnvelope.cs
    ProtocolEventNames.cs
  Observability/
    ProtocolActivitySource.cs
  Streaming/
    EventStreamEndpoint.cs

backend/tests/ChromaAgentics.Backend.Tests/
  Persistence/
  Events/
  Acknowledgements/
  Protocol/
  Streaming/

docs/
  API_CONTRACT.md
  GETTING_STARTED_BACKEND.md
  PHASE_02_SPRINT_02_REPORT.md
  schemas/protocol/v0.2/
    envelope.schema.json
    workflow-start.schema.json
    session-resume.schema.json
    event-ack.schema.json
    error-envelope.schema.json

Optional but recommended:

docs/ADRs/ADR-0001-phase-2-protocol-support-tables.md
Required Data Model

Add EF Core entities and migrations for these Phase 2 protocol-support tables.

WorkflowSessions and EventAcknowledgements are protocol-support tables. They extend the target architecture for WebSocket durability and replay. They do not replace the long-term schema.

Workspaces
Id uuid primary key
Name text nullable
CreatedAtUtc timestamptz not null
UpdatedAtUtc timestamptz nullable
WorkflowExecutions
Id uuid primary key
WorkspaceId uuid not null foreign key -> Workspaces.Id
Status text not null
Title text nullable
Mode text nullable
Source text nullable
NextSequence bigint not null default 1
CreatedAtUtc timestamptz not null
UpdatedAtUtc timestamptz not null
CancelledAtUtc timestamptz nullable
CancellationReason text nullable

Allowed statuses:

created
running
cancelled
completed
failed

Sprint 2 core should use only:

created
running

cancelled is stretch only.

WorkflowSessions
Id uuid primary key
WorkspaceId uuid not null foreign key -> Workspaces.Id
WorkflowId uuid not null foreign key -> WorkflowExecutions.Id
CreatedAtUtc timestamptz not null
LastConnectedAtUtc timestamptz not null
ClosedAtUtc timestamptz nullable
ClientName text nullable
ExecutionEvents
Id uuid primary key
WorkspaceId uuid not null foreign key -> Workspaces.Id
WorkflowId uuid not null foreign key -> WorkflowExecutions.Id
SessionId uuid nullable foreign key -> WorkflowSessions.Id
Sequence bigint not null
Name text not null
ProtocolVersion text not null
MessageId uuid not null
CorrelationId uuid nullable
CausationMessageId uuid nullable
IdempotencyKey text nullable
PayloadHash text nullable
PayloadJson jsonb not null
CreatedAtUtc timestamptz not null
EventAcknowledgements
Id uuid primary key
WorkspaceId uuid not null foreign key -> Workspaces.Id
WorkflowId uuid not null foreign key -> WorkflowExecutions.Id
SessionId uuid not null foreign key -> WorkflowSessions.Id
LastSeenSequence bigint not null
UpdatedAtUtc timestamptz not null
Required Constraints and Indexes

Implement:

unique (WorkflowId, Sequence)
unique (WorkflowId, MessageId)
unique (WorkflowId, Name, IdempotencyKey) where IdempotencyKey is not null
unique (WorkflowId, SessionId)
index (WorkflowId, Sequence)
index (SessionId)
index (WorkspaceId)
index (IdempotencyKey)
index (CreatedAtUtc)

If EF Core/Npgsql makes a filtered unique index awkward, implement the closest safe equivalent and document the tradeoff.

Migration Requirements

Create one Sprint 2 migration for protocol-support persistence.

Preferred migration name:

Sprint02ProtocolSupport

Expected command shape:

dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend

Adjust paths only if repo structure requires it.

Migration must create only Sprint 2 protocol-support tables.

Do not add:

RAG tables
pgvector retrieval tables
embedding tables
provider config tables
tool-call execution tables
patch-set execution tables
approval execution tables

Contract placeholders are allowed only when clearly marked future-facing and not wired into runtime behavior.

Transaction-Safe Event Append

Preferred implementation:

WorkflowExecutions.NextSequence

Append algorithm:

1. Begin database transaction.
2. Lock the WorkflowExecutions row.
3. Read NextSequence.
4. Assign that value to ExecutionEvents.Sequence.
5. Increment WorkflowExecutions.NextSequence.
6. Insert ExecutionEvent.
7. Commit.

Acceptable fallback:

Use unique (WorkflowId, Sequence) plus retry-on-conflict.

Document which strategy was implemented.

Sequence numbers must be:

monotonic per workflow
unique per workflow
stable once persisted
used for replay ordering
Required Services

Implement or update:

IEventStore
PostgresEventStore
IAcknowledgementStore
PostgresAcknowledgementStore
IWorkflowProtocolService
WorkflowProtocolService
IProtocolMessageValidator
ProtocolMessageValidator
ProtocolErrorFactory
IEventStore must support
AppendEventAsync
GetEventsAfterSequenceAsync
GetMaxSequenceAsync
GetEventByIdempotencyKeyAsync
GetEventByMessageIdAsync
IAcknowledgementStore must support
GetLastSeenSequenceAsync
UpdateLastSeenSequenceAsync
IWorkflowProtocolService must support
StartWorkflowAsync
ResumeSessionAsync
AcknowledgeEventsAsync

Stretch only:

CancelWorkflowAsync
Protocol Envelope

All inbound protocol messages use:

{
  "protocolVersion": "0.2",
  "messageId": "uuid",
  "workspaceId": "uuid",
  "workflowId": "uuid-or-null",
  "sessionId": "uuid",
  "sequence": null,
  "name": "message.name",
  "correlationId": "uuid-or-null",
  "idempotencyKey": "string-or-null",
  "timestamp": "ISO-8601",
  "payload": {}
}

Persisted outbound events must include assigned sequence:

{
  "protocolVersion": "0.2",
  "messageId": "uuid",
  "workspaceId": "uuid",
  "workflowId": "uuid",
  "sessionId": "uuid",
  "sequence": 1,
  "name": "workflow.started",
  "correlationId": "uuid-or-null",
  "idempotencyKey": "string-or-null",
  "timestamp": "ISO-8601",
  "payload": {}
}
Required Inbound Messages
workflow.start
session.resume
event.ack
Required Outbound Messages
connection.ready
workflow.started
workflow.status
error

Replayed events must be sent using their original event names and original sequence numbers.

Do not persist synthetic replay events.

Avoid event.replayed unless it is explicitly non-durable and documented. Prefer original event replay.

workflow.start

When handling workflow.start:

validate dev token
validate envelope
validate protocolVersion = 0.2
validate required IDs
create workspace only if explicitly documented
create workflow execution
create workflow session if missing
append workflow.started
append workflow.status
emit persisted events to client
enforce idempotency

Payload:

{
  "title": "Smoke test workflow",
  "mode": "orchestrator",
  "source": "manual-smoke-test"
}

Idempotency rules:

same workflow/message type/idempotency key + same payload hash:
return same workflowId and previously persisted workflow.started/workflow.status events

same workflow/message type/idempotency key + different payload hash:
return error envelope with code idempotency_conflict

missing idempotency key:
allowed, but no duplicate protection
session.resume

When handling session.resume:

validate dev token
validate workflow/session
read lastSeenSequence

Then act according to `lastSeenSequence`:

| lastSeenSequence | action |
|---|---|
| `0` | replay all persisted workflow events |
| between first and latest | replay persisted events where `sequence > lastSeenSequence` |
| equal latest | emit a documented resume status with no prior events |
| greater than latest | return `future_sequence` error |

Preserve ascending sequence order.
Do not create new `ExecutionEvents` for replayed events.

Payload:

{
  "lastSeenSequence": 3
}
event.ack

When handling event.ack:

validate workflow/session
get current max workflow sequence
reject lastSeenSequence > maxSequence with future_ack
if lastSeenSequence <= currentAck, no-op
if lastSeenSequence > currentAck, update ACK state
emit safe status or no response, but document chosen behavior
do not create a durable event unless explicitly justified and documented

Payload:

{
  "lastSeenSequence": 5
}

ACK means:

client received and processed protocol events up to LastSeenSequence

ACK does not mean:

approval
permission
file edit execution
terminal execution
tool execution
MCP execution
Error Handling

All recoverable protocol errors must return a safe error envelope.

Error envelope shape:

{
  "protocolVersion": "0.2",
  "messageId": "uuid",
  "workspaceId": "uuid-or-null",
  "workflowId": "uuid-or-null",
  "sessionId": "uuid-or-null",
  "sequence": null,
  "name": "error",
  "correlationId": "uuid-or-null",
  "idempotencyKey": null,
  "timestamp": "ISO-8601",
  "payload": {
    "code": "string",
    "message": "safe human-readable summary",
    "retryable": false
  }
}

Required error codes:

invalid_json
bad_protocol_version
unknown_message_name
missing_required_field
invalid_id
workflow_not_found
session_not_found
idempotency_conflict
future_ack
future_sequence
workflow_cancelled
unauthorized
internal_error

Error payloads must not contain:

tokens
connection strings
passwords
raw prompts
provider keys
raw stack traces
full payload bodies
raw upstream responses
Security Rules

Preserve Sprint 1 security behavior:

localhost-first binding
CHROMA_ALLOW_LAN_BINDING required for non-loopback host
X-Chroma-Dev-Token required for WebSocket auth
query-string devToken allowed only for documented smoke testing if Sprint 1 already allowed it
no broad CORS
no secrets in logs, responses, or errors

Do not log:

payload bodies
tokens
prompts
provider keys
full DB connection strings
passwords
raw upstream responses

Log metadata only:

workflowId
sessionId
sequence
message name
correlationId
result status
error code
Observability

Add structured logs for:

websocket.connection.accepted
websocket.connection.rejected
protocol.message.received
protocol.message.rejected
workflow.started
event.appended
event.replayed
event.ack.updated
event.ack.noop

Add an ActivitySource:

ChromaAgentics.Backend.Protocol

Add spans around:

workflow.start
event.append
session.resume
event.replay
event.ack

No external telemetry exporter is required in Sprint 2.

JSON Schema / Contract Artifacts

Create starter JSON Schema files for protocol 0.2:

docs/schemas/protocol/v0.2/envelope.schema.json
docs/schemas/protocol/v0.2/workflow-start.schema.json
docs/schemas/protocol/v0.2/session-resume.schema.json
docs/schemas/protocol/v0.2/event-ack.schema.json
docs/schemas/protocol/v0.2/error-envelope.schema.json

Schemas must match implemented behavior.

Tests

Split tests into:

unit tests
integration tests
WebSocket contract tests
manual smoke tests

Preferred integration strategy:

Testcontainers PostgreSQL

If Testcontainers is not practical, document fallback:

Docker Compose PostgreSQL
configured local PostgreSQL

Do not require live Ollama for Sprint 2 tests.

Unit tests must cover
protocol envelope validation
bad protocol version
unknown message name
missing required IDs
invalid UUIDs
ACK rule logic
idempotency conflict logic
error envelope creation
sequence assignment logic where mockable
redaction behavior
Integration tests must cover
EF migration applies
event append persists row
WorkflowExecutions.NextSequence increments
unique (WorkflowId, Sequence) enforced
replay returns sequence > lastSeenSequence
duplicate idempotency key does not create duplicate event
same idempotency key + different payload returns conflict
ACK persists and updates cumulatively
future ACK returns error
WebSocket contract tests must cover
auth failure
invalid JSON
unknown message name
missing required IDs
bad protocolVersion
workflow.start success
resume from 0
resume from middle sequence
resume from latest sequence
duplicate idempotency key
idempotency conflict with changed payload
duplicate/lower ACK no-op
future ACK error
Documentation Requirements

Update:

docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md

Create:

docs/PHASE_02_SPRINT_02_REPORT.md
docs/schemas/protocol/v0.2/*.schema.json

Optional but recommended:

docs/ADRs/ADR-0001-phase-2-protocol-support-tables.md

docs/API_CONTRACT.md must document:

protocol version 0.2
implemented inbound messages
implemented outbound messages
envelope shape
workflow.start example
session.resume example
event.ack example
error envelope shape
idempotency rules
ACK rules
resume/replay rules
auth behavior
schema file locations
planned-only features

Clearly mark these as planned only:

protocol 1.0
workflow.cancel unless implemented as stretch
approval execution
full extension bridge
Microsoft Agent Framework workflows
Ollama chat
model streaming
model discovery
pgvector
RAG
tool execution
file edit execution
terminal execution
MCP execution
Next.js dashboard
LangGraph
n8n

docs/PHASE_02_SPRINT_02_REPORT.md must include:

sprint goal
Sprint 1 gate result
final scope
package versions
database migration summary
schema summary
files changed
commands run
validation results
test results
WebSocket behavior
replay behavior
ACK behavior
known gaps
risks
stretch work status
next sprint recommendation
Validation Commands

Run:

dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config

Run migration validation using the exact implemented command. Expected shape:

dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend

If safe:

docker compose up --build

Manual smoke:

curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies

Also run a WebSocket smoke test that:

1. connects with valid X-Chroma-Dev-Token
2. sends workflow.start
3. receives persisted workflow events
4. sends event.ack
5. disconnects
6. reconnects
7. sends session.resume
8. verifies missed events replay in order

Report exact command outputs or summarized key lines.

Explicitly Out of Scope

Do not implement:

Microsoft Agent Framework workflows
real agent planning
approval request execution
approval decision execution
file edit execution
terminal command execution
MCP tool execution
Ollama chat adapter
Ollama model discovery
model.stream
pgvector extension setup
RAG ingestion
RAG retrieval
embeddings
Next.js dashboard
LangGraph
n8n
cloud provider adapters
production authentication
multi-user authorization
full VS Code extension UI integration
Phase 2 completion claim

Contract placeholders are allowed only when clearly labeled as future-facing.

Definition of Done

Sprint 2 is complete only when:

Sprint 1 gate is verified or blockers are documented
backend builds
tests pass
EF Core persistence exists
migration applies
protocol-support tables exist
event append is durable
sequence assignment is transaction-safe
workflow.start persists workflow and events
session.resume replays only missed events
event.ack updates cumulative ACK state
idempotency is enforced
safe error envelopes exist
WebSocket auth remains enforced
localhost-first config remains intact
health endpoints from Sprint 1 still pass
starter JSON Schemas exist for protocol 0.2
docs/API_CONTRACT.md reflects implementation
docs/PHASE_02_SPRINT_02_REPORT.md exists
deferred features are not falsely claimed

Stretch complete only if:

workflow.cancel works, is idempotent, documented, and tested
Required Final Report

End your work with a concise report:

Summary
Sprint 1 gate result
Files created/modified
Package versions
Migration behavior
Commands run
Test results
WebSocket behavior
Replay behavior
ACK behavior
Idempotency behavior
Security/observability notes
JSON Schema/docs updates
Known gaps
Stretch work status
Next sprint recommendation

Be honest. Durable protocol correctness matters more than a shiny pile of half-working code.
```
