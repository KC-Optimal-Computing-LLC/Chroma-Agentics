# Code Generation Prompt: Phase 2 Sprint 2 — Durable Event Store + Resume/Replay

You are working in the Chroma Agentics repository.

Implement Phase 2 Sprint 2: Durable Event Store + Resume/Replay.

This sprint extends the Sprint 1 backend foundation with PostgreSQL-backed protocol durability, event sequencing, session resume, replay, and cumulative ACK handling.

Do not expand scope beyond Sprint 2. Do not implement systems planned for Phase 3 or beyond, such as real approval execution, extension bridge UI, Ollama chat adapters, pgvector/RAG, or full Microsoft Agent Framework workflows.

`cancelled` may be documented as a future stretch goal only; do not implement cancellation behavior unless explicitly approved.

---

## Source of Truth

Use:

- `README.md`
- `ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md`
- `docs/API_CONTRACT.md`
- `docs/PHASE_02_SPRINT_01_REPORT.md`
- existing Sprint 1 backend implementation

The architecture requires:

- local-first defaults
- PostgreSQL-backed persistence
- WebSocket protocol transport
- localhost-first security
- dev-token WebSocket auth
- extension-side approval and action execution
- backend-side durable protocol state

The backend must not execute file edits, terminal commands, MCP actions, or approval decisions.

---

## Sprint 1 Gate

Before implementing Sprint 2:

1. Inspect Sprint 1 implementation.
2. Confirm Sprint 1 backend builds.
3. Confirm Sprint 1 tests pass.
4. Confirm health endpoints still exist.
5. Confirm `/ws/events` exists and enforces dev-token auth.
6. Confirm `docker-compose.yml` and `.env.example` exist.
7. Confirm Sprint 1 report exists.

If any Sprint 1 requirement is missing, stop and report blockers unless the issue is clearly non-blocking and documented.

Run or verify:

```bash
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
Sprint Goal

Implement the smallest durable protocol slice:

EF Core PostgreSQL persistence.
Minimal protocol-support schema.
Transaction-safe event append.
Monotonic per-workflow sequence numbers.
Required message flows:
- `workflow.start`: validate auth, envelope, protocol version, required IDs, and persist the start event.
- `session.resume`: validate auth, restore last seen sequence, and resume the workflow stream.
- `event.ack`: validate acknowledgements, update persisted last-seen sequence, and support replay progress.
Ordered replay of events where sequence > lastSeenSequence.
Idempotency handling.
Safe structured error envelopes.
Structured logs and minimal ActivitySource spans.
Protocol 0.2 docs and starter JSON Schemas.
Sprint 2 report.

Use:

protocolVersion = "0.2"

Do not claim protocol 1.0.

Required First Step: Inspect

Before editing, inspect and report:

Sprint 1 backend structure.
Existing health/config/WebSocket files.
Existing protocol envelope types.
Existing package versions.
Existing Docker/PostgreSQL setup.
Existing docs.
Existing tests.
Conflicts with this sprint plan.

Then implement.

Required Package Pins

Target net8.0.

Pin all new packages exactly. Use compatible latest stable versions only if these exact versions are unavailable, and document the reason.

Required EF/PostgreSQL packages:

Microsoft.EntityFrameworkCore
Microsoft.EntityFrameworkCore.Design
Npgsql.EntityFrameworkCore.PostgreSQL

Preferred integration test package:

Testcontainers.PostgreSql

Also preserve Sprint 1 package pins unless there is a documented compatibility reason to change them.

Document all package versions in:

docs/PHASE_02_SPRINT_02_REPORT.md
Required Data Model

Add EF Core entities and migrations for these Phase 2 protocol-support tables.

Core protocol-support entities:
- `Workspaces`, `WorkflowExecutions`, `WorkflowSessions`, `ExecutionEvents`, and `EventAcknowledgements` support durable event streams, replay, and resume.
- These tables extend the target architecture for WebSocket durability and replay without replacing the long-term application schema.

WorkflowSessions and EventAcknowledgements are protocol-support tables used only for durability and replay state. Keep Sprint 2 scope limited to protocol durability support.

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

If filtered unique indexes are awkward in EF Core/Npgsql, implement the closest safe equivalent and document it.

Migration Requirements

Create one initial Sprint 2 migration for protocol-support persistence.

Preferred migration name:

Sprint02ProtocolSupport

Document exact migration commands used.

Expected command shape:

dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend

Adjust paths only if repo structure requires it.

Migration must create only Sprint 2 protocol-support tables. Do not add RAG, pgvector, provider config, tool calls, patch sets, or approval execution tables unless explicitly marked as future placeholders and not wired.

Transaction-Safe Event Append

Preferred implementation:

WorkflowExecutions.NextSequence

Append algorithm:

1. Begin database transaction.
2. Lock WorkflowExecutions row for the workflow.
3. Read NextSequence.
4. Assign that value to ExecutionEvents.Sequence.
5. Increment WorkflowExecutions.NextSequence.
6. Insert ExecutionEvent.
7. Commit.

Acceptable fallback:

Use unique (WorkflowId, Sequence) plus retry-on-conflict.

Document which strategy was implemented.

Sequence must be:

monotonic per workflow
unique per workflow
stable after persistence
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

Replayed events should be sent with their original event names and original sequence numbers.

Do not persist synthetic replay events.

Optional non-durable message after replay:

workflow.status

Only use `event.replayed` as a non-durable replay annotation during live replay delivery. Do not write `event.replayed` into durable storage, and document its non-durable behavior clearly. Prefer not to use it.

workflow.start

Required behavior:

Validate dev token.
Validate envelope.
Validate protocol version 0.2.
Validate required IDs.
Create workspace only if explicitly documented.
Create workflow execution.
Create workflow session if missing.
Append workflow.started.
Append workflow.status.
Emit persisted events to client.
Preserve idempotency.

Payload:

{
  "title": "Smoke test workflow",
  "mode": "orchestrator",
  "source": "manual-smoke-test"
}

Idempotent retry behavior:

same workflow/message type/idempotency key + same payload hash:
return same workflowId and previously persisted workflow.started/workflow.status events

same workflow/message type/idempotency key + different payload hash:
return error envelope with code idempotency_conflict

missing idempotency key:
allowed, but no duplicate protection
session.resume

Required behavior:

Validate dev token.
Validate workflow/session.
Read lastSeenSequence.
If lastSeenSequence = 0, replay all workflow events.
If middle sequence, replay events where sequence > lastSeenSequence.
If latest sequence, replay no prior events and return documented status.
If future sequence, return future_sequence error.
Preserve ascending sequence order.
Do not create new ExecutionEvents for replayed events.

Payload:

{
  "lastSeenSequence": 3
}
event.ack

Required behavior:

Validate workflow/session.
Get current max workflow sequence.
Reject lastSeenSequence > maxSequence with future_ack.
If lastSeenSequence <= currentAck, no-op.
If lastSeenSequence > currentAck, update ACK state.
Emit safe status or no response, but document chosen behavior.
Do not create a durable event unless explicitly justified and documented.

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
Error Envelope

All recoverable protocol errors return:

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

Errors must not include:

tokens
connection strings
passwords
raw prompts
provider keys
raw stack traces
full payload contents
raw upstream responses
Security Rules

Preserve Sprint 1 security:

localhost-first binding
CHROMA_ALLOW_LAN_BINDING required for non-loopback host
X-Chroma-Dev-Token required for WebSocket auth
query-string devToken allowed only for documented smoke testing if Sprint 1 already allowed it
no broad CORS
no secrets in logs/responses/errors

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

Create starter schema files for protocol 0.2:

docs/schemas/protocol/v0.2/envelope.schema.json
docs/schemas/protocol/v0.2/workflow-start.schema.json
docs/schemas/protocol/v0.2/session-resume.schema.json
docs/schemas/protocol/v0.2/event-ack.schema.json
docs/schemas/protocol/v0.2/error-envelope.schema.json

Schemas must match implemented behavior.

Test Strategy

Split tests into:

Unit tests
Integration tests
WebSocket contract tests
Manual smoke tests

Preferred integration approach:

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
Manual smoke tests must cover
docker compose up --build
health endpoints still work
WebSocket connect with X-Chroma-Dev-Token
workflow.start
event.ack
disconnect/reconnect
session.resume
ordered replay verification
Documentation Requirements

Update:

docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md

Create:

docs/PHASE_02_SPRINT_02_REPORT.md
docs/schemas/protocol/v0.2/*.schema.json

Optional but recommended:

docs/ADRs/ADR-0001-phase-2-protocol-support-tables.md

docs/API_CONTRACT.md must include:

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

If PostgreSQL or another required dependency is unavailable, record the startup failure and verify the backend reports the dependency issue gracefully rather than crashing.

Manual smoke:

curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies

Also run a WebSocket smoke test that:

connects with valid X-Chroma-Dev-Token
sends workflow.start
receives persisted workflow events
sends event.ack
disconnects
reconnects
sends session.resume
verifies missed events replay in order

Report exact command outputs or summarized key lines.

Hard Constraints

Do not implement:

MAF workflows
Ollama chat
model discovery
model streaming
RAG
pgvector retrieval
approval execution
file edit execution
terminal execution
MCP execution
extension UI
Next.js
LangGraph
n8n
cloud providers
production auth
multi-user authorization
Phase 2 completion claim

Do not falsely claim:

protocol 1.0 complete
extension bridge complete
approval flow complete
durable orchestration complete
RAG complete
Ollama provider complete
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
Final Output Format

When complete, output:

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

Be honest. Durable protocol correctness matters more than an impressive-looking pile of half-working code.
```
