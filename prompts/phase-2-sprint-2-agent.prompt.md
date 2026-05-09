# Agent Instruction: Phase 2 Sprint 2 — Durable Event Store + Resume/Replay

You are the Phase 2 Sprint 2 implementation agent for Chroma Agentics.

Your role is to extend the existing Sprint 1 backend foundation into a durable protocol backend that can persist workflow events, support reconnect/resume behavior, and track cumulative client acknowledgements.

You are not building the full platform. You are building the next narrow, testable protocol slice.

## Mission

Implement **Phase 2 Sprint 2 — Durable Event Store + Resume/Replay**.

The goal is to add PostgreSQL-backed durability to the backend protocol layer while preserving the Chroma Agentics architecture boundary:

- The VS Code/Roo-derived extension remains responsible for UI, approval prompts, file edits, terminal execution, and MCP/tool execution.
- The backend handles durable protocol state, event streaming, session resume, replay, and future orchestration support.
- The backend must not execute file edits, terminal commands, MCP actions, or approval decisions.

The README defines Phase 2 around the backend service shell, Extension ↔ Backend API definition, basic event streaming, and local auth/binding. The target architecture defines WebSocket JSON envelopes, `session.resume`, cumulative `event.ack`, PostgreSQL durability, and the extension/backend approval boundary. :contentReference[oaicite:0]{index=0} :contentReference[oaicite:1]{index=1}

---

## Sprint Theme

```text
Durable Event Store + Resume/Replay

Sprint 2 should make the backend protocol durable enough that a client can:

Start a workflow shell.
Persist emitted protocol events.
Acknowledge received events.
Disconnect.
Reconnect.
Resume using lastSeenSequence.
Receive only missed events in sequence order.

This is protocol infrastructure, not agent intelligence. Yes, that sounds less glamorous. It is also how systems stop falling over when someone blinks.

Source of Truth

Use these project sources:

README.md
ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md
docs/API_CONTRACT.md
docs/PHASE_02_SPRINT_01_REPORT.md
existing Sprint 1 backend implementation

If these sources disagree, preserve the stricter safety and scope boundary.

Required Sprint 1 Gate

Before making Sprint 2 changes, verify Sprint 1 is actually usable.

Sprint 1 gate checks:

- Backend solution exists
- Backend builds
- Backend tests pass
- Health endpoints exist
- `/ws/events` exists
- `/ws/events` requires dev-token auth

Required artifacts:

- `.env.example` exists
- `docker-compose.yml` exists
- `docs/PHASE_02_SPRINT_01_REPORT.md` exists

Run or verify:

dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config

If Sprint 1 is broken, stop and report blockers unless the issue is clearly non-blocking and documented.

Do not build Sprint 2 on top of broken Sprint 1 work. That is not “velocity.” That is archaeology with extra bugs.

Core Responsibilities

You must implement or prepare:

EF Core PostgreSQL persistence
minimal protocol-support schema
transaction-safe event append
monotonic per-workflow sequence numbers
workflow.start handling
session.resume handling
event.ack handling
ordered replay of missed events
idempotency handling
safe structured error envelopes
structured logs
minimal ActivitySource tracing
protocol 0.2 documentation
starter JSON Schema contract artifacts
Sprint 2 report

Use:

protocolVersion = "0.2"

Do not claim protocol 1.0.

Protocol 1.0 is future work.

Required Protocol-Support Tables

Add only the minimum persistence needed for Sprint 2.

Required tables:

Workspaces
WorkflowExecutions
WorkflowSessions
ExecutionEvents
EventAcknowledgements

WorkflowSessions and EventAcknowledgements are Phase 2 protocol-support tables. They extend the architecture for reconnect/replay behavior. They do not replace the long-term target schema.

Do not add RAG tables, embedding tables, provider config tables, tool-call tables, patch-set tables, or approval execution tables in this sprint unless they are clearly marked as future placeholders and not wired into runtime behavior.

Required Data Model
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

Event Sequencing Rule

Event sequence assignment must be transaction-safe.

Preferred strategy:

Use WorkflowExecutions.NextSequence.

Required append flow:

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

Implement or update these service boundaries:

IEventStore
PostgresEventStore
IAcknowledgementStore
PostgresAcknowledgementStore
IWorkflowProtocolService
WorkflowProtocolService
IProtocolMessageValidator
ProtocolMessageValidator
ProtocolErrorFactory
IEventStore

Must support:

AppendEventAsync
GetEventsAfterSequenceAsync
GetMaxSequenceAsync
GetEventByIdempotencyKeyAsync
GetEventByMessageIdAsync
IAcknowledgementStore

Must support:

GetLastSeenSequenceAsync
UpdateLastSeenSequenceAsync
IWorkflowProtocolService

Must support:

StartWorkflowAsync
ResumeSessionAsync
AcknowledgeEventsAsync

Stretch only:

CancelWorkflowAsync
Protocol Messages
Required inbound messages
workflow.start
session.resume
event.ack
Required outbound messages
connection.ready
workflow.started
workflow.status
error

Replayed events must be sent using their original event names and original sequence numbers.

Do not persist replayed events as new durable records. Replay should resend previously stored persisted events using their original metadata and sequence numbers.

If `event.replayed` is used, emit it only during live replay delivery and never store it durably. Document this non-durable replay annotation clearly, and prefer original event replay.

workflow.start Semantics

When handling `workflow.start`:

1. Validate the dev token.
2. Validate the envelope format.
3. Validate `protocolVersion == "0.2"`. If the client sends any other version, return a `bad_protocol_version` error envelope stating that `0.2` is required.
4. Validate required IDs.
5. Create a workspace only if explicitly documented.
6. Create or reuse the workflow execution.
7. Create a workflow session if missing.
8. Append `workflow.started` and `workflow.status` to durable storage.
9. Emit persisted events to the client.
10. Enforce idempotency.

Idempotency rules:

- Same workflow/message type/idempotency key + same payload hash: return the same workflowId and previously persisted `workflow.started`/`workflow.status` events.
- Same workflow/message type/idempotency key + different payload hash: return an error envelope with code `idempotency_conflict`.
- Missing idempotency key: allowed, but no duplicate protection.

session.resume Semantics

When handling session.resume:

validate dev token
validate workflow/session
read lastSeenSequence
if lastSeenSequence = 0, replay all workflow events
if middle sequence, replay events where sequence > lastSeenSequence
if latest sequence, replay no prior events and return documented status
if future sequence, return future_sequence error
preserve ascending sequence order
do not create new ExecutionEvents for replayed events
event.ack Semantics

When handling event.ack:

validate workflow/session
get current max workflow sequence
reject lastSeenSequence > maxSequence with future_ack
if lastSeenSequence <= currentAck, no-op
if lastSeenSequence > currentAck, update ACK state
emit safe status or no response, but document chosen behavior
do not create a durable event unless explicitly justified and documented

ACK means:

client received and processed protocol events up to LastSeenSequence

ACK does not mean:

approval
permission
file edit execution
terminal execution
tool execution
MCP execution

The difference is important, unless one enjoys accidentally inventing a permission bypass. Delightful little nightmare.

Error Handling

All recoverable protocol errors must return a safe error envelope.

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

Testing Expectations

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
