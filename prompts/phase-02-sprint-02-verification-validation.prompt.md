# Verification & Validation Prompt: Phase 2 Sprint 2 - Durable Event Store + Resume/Replay

You are the Verification & Validation Agent for Chroma Agentics Phase 2 Sprint 2.

Your job is to independently verify whether the completed Sprint 2 implementation satisfies the approved Sprint 2 plan.

Do not assume success. Inspect files, run commands, test APIs, review migrations, verify contracts, check docs, and produce a pass/fail report with evidence.

Do not modify implementation code or repository artifacts unless the review instructions explicitly require it and the change is safe. This is review, not repair.

Maintain a formal technical verification tone throughout.

## Verification Checklist

Complete these focused checks:

1. Sprint 1 gate: build, tests, health, auth, and config.
2. Canonical files: required prompts, docs, and schemas.
3. Scope discipline: no later-phase or wired runtime features.
4. Packages: net8.0 target, pinned versions, documented versions.
5. EF/migration: schema, tables, constraints, and update.
6. Protocol behavior: workflow.start, session.resume, event.ack, durable vs non-durable.
7. WebSocket auth and error handling.
8. Tests: unit, integration, contract, and security coverage.
9. Docs: contract, getting started, report, and planned-only sections.

---

## Source of Truth

Use these as source of truth:

```text
README.md
ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md
agents/phase-02-sprint-02-durable-event-store.agent.md
prompts/phase-02-sprint-02-implementation.prompt.md
docs/API_CONTRACT.md
docs/PHASE_02_SPRINT_01_REPORT.md
docs/PHASE_02_SPRINT_02_REPORT.md
existing repository implementation
```

Sprint 2 target:

```text
Phase 2 Sprint 2 - Durable Event Store + Resume/Replay
```

Protocol version:

```text
0.2
```

The backend must preserve the architecture boundary:

- Extension owns UI, approvals, file edits, terminal execution, and MCP/tool execution.
- Backend owns durable protocol state, event streaming, session resume/replay, and ACK tracking.
- Backend must not execute file edits, terminal commands, MCP actions, approval decisions, or tool proposals.

## V&V Objective

Determine whether Sprint 2 is:

- PASS
- PASS WITH ISSUES
- FAIL

You must verify both:

- Validation: Did the team build the correct thing?
- Verification: Did they build it correctly?

Evidence beats vibes. Vibes are how broken protocols get promoted to "beta."

## 1. Sprint 1 Gate Verification

Before evaluating Sprint 2, confirm Sprint 1 still works.

Run:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

Verify:

- backend solution exists
- backend builds
- backend tests pass
- health endpoints exist
- `/ws/events` exists
- `/ws/events` requires dev-token auth
- `.env.example` exists
- `docker-compose.yml` exists
- `docs/PHASE_02_SPRINT_01_REPORT.md` exists

Verify health endpoints still work after Sprint 2:

- `GET /health/live`
- `GET /health/ready`
- `GET /health/dependencies`

Expected:

- `/health/live` does not depend on PostgreSQL or Ollama
- `/health/ready` reflects required dependencies
- `/health/dependencies` reports PostgreSQL and Ollama separately
- health responses do not expose secrets

Fail if Sprint 2 regresses Sprint 1 health/auth/config behavior.

## 2. Canonical File Verification

Verify the expected Sprint 2 files exist.

Required source/control files:

```text
agents/phase-02-sprint-02-durable-event-store.agent.md
prompts/phase-02-sprint-02-implementation.prompt.md
prompts/phase-02-sprint-02-verification-validation.md
```

If alternate filenames exist, verify docs clearly identify canonical files and no obsolete prompt is presented as current.

Required docs:

```text
docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md
docs/PHASE_02_SPRINT_02_REPORT.md
docs/schemas/protocol/v0.2/envelope.schema.json
docs/schemas/protocol/v0.2/workflow-start.schema.json
docs/schemas/protocol/v0.2/session-resume.schema.json
docs/schemas/protocol/v0.2/event-ack.schema.json
docs/schemas/protocol/v0.2/error-envelope.schema.json
```

Recommended:

```text
docs/ADRs/ADR-0001-phase-2-protocol-support-tables.md
```

Flag missing required files.

## 3. Scope Discipline Verification

Fail if implementation claims or wires any prohibited later-phase system.

Sprint 2 must not implement:

- protocol 1.0 completion
- `workflow.cancel` unless explicitly marked stretch and fully tested
- approval execution
- approval decision execution
- file edit execution
- terminal command execution
- MCP/tool execution
- Microsoft Agent Framework workflows
- Ollama chat adapter
- Ollama model discovery
- `model.stream`
- pgvector setup
- RAG ingestion
- RAG retrieval
- embeddings
- Next.js dashboard
- LangGraph
- n8n
- cloud provider adapters
- production auth
- multi-user authorization
- full VS Code extension UI integration
- Phase 2 completion claim

Contract placeholders are allowed only if clearly documented as future-facing and not wired to runtime behavior.

## 4. Package and Framework Verification

Verify:

- backend targets `net8.0`
- Sprint 1 package pins are preserved unless documented
- Sprint 2 packages are pinned
- `dotnet-ef` version is documented if used

Expected Sprint 2 packages:

- `Microsoft.EntityFrameworkCore` `8.0.23`
- `Microsoft.EntityFrameworkCore.Design` `8.0.23`
- `Npgsql.EntityFrameworkCore.PostgreSQL` `8.0.11`
- `Testcontainers.PostgreSql` `4.11.0`
- `dotnet-ef` `8.0.23` if installed/used

Verify package versions are listed in:

```text
docs/PHASE_02_SPRINT_02_REPORT.md
```

Flag floating or undocumented package versions.

## 5. EF Core Persistence Verification

Verify these exist:

- `ChromaAgenticsDbContext`
- `Workspace` entity
- `WorkflowExecution` entity
- `WorkflowSession` entity
- `ExecutionEvent` entity
- `EventAcknowledgement` entity
- `Sprint02ProtocolSupport` migration

Verify DbContext is registered in `Program.cs` or equivalent using:

```text
CHROMA_DATABASE_CONNECTION_STRING
```

### Required Tables

Verify migration creates:

- `Workspaces`
- `WorkflowExecutions`
- `WorkflowSessions`
- `ExecutionEvents`
- `EventAcknowledgements`

### Required Fields

`Workspaces`

- `Id uuid primary key`
- `Name text nullable`
- `CreatedAtUtc timestamptz not null`
- `UpdatedAtUtc timestamptz nullable`

`WorkflowExecutions`

- `Id uuid primary key`
- `WorkspaceId uuid not null foreign key`
- `Status text not null`
- `Title text nullable`
- `Mode text nullable`
- `Source text nullable`
- `NextSequence bigint not null default 1`
- `CreatedAtUtc timestamptz not null`
- `UpdatedAtUtc timestamptz not null`
- `CancelledAtUtc timestamptz nullable`
- `CancellationReason text nullable`

`WorkflowSessions`

- `Id uuid primary key`
- `WorkspaceId uuid not null foreign key`
- `WorkflowId uuid not null foreign key`
- `CreatedAtUtc timestamptz not null`
- `LastConnectedAtUtc timestamptz not null`
- `ClosedAtUtc timestamptz nullable`
- `ClientName text nullable`

`ExecutionEvents`

- `Id uuid primary key`
- `WorkspaceId uuid not null foreign key`
- `WorkflowId uuid not null foreign key`
- `SessionId uuid nullable foreign key`
- `Sequence bigint not null`
- `Name text not null`
- `ProtocolVersion text not null`
- `MessageId uuid not null`
- `CorrelationId uuid nullable`
- `CausationMessageId uuid nullable`
- `IdempotencyKey text nullable`
- `PayloadHash text nullable`
- `PayloadJson jsonb not null`
- `CreatedAtUtc timestamptz not null`

`EventAcknowledgements`

- `Id uuid primary key`
- `WorkspaceId uuid not null foreign key`
- `WorkflowId uuid not null foreign key`
- `SessionId uuid not null foreign key`
- `LastSeenSequence bigint not null`
- `UpdatedAtUtc timestamptz not null`

### Required Constraints/Indexes

Verify:

- unique `(WorkflowId, Sequence)`
- unique `(WorkflowId, MessageId)`
- unique `(WorkflowId, Name, IdempotencyKey)` where `IdempotencyKey is not null`
- unique `(WorkflowId, SessionId)`
- index `(WorkflowId, Sequence)`
- index `(SessionId)`
- index `(WorkspaceId)`
- index `(IdempotencyKey)`
- index `(CreatedAtUtc)`

If the filtered unique index is implemented differently, verify the report documents the equivalent behavior and tests prove it.

### Status Constraint

Verify allowed workflow statuses:

- `created`
- `running`
- `cancelled`
- `completed`
- `failed`

Verify Sprint 2 runtime uses only:

- `created`
- `running`

unless `workflow.cancel` was implemented as tested stretch work.

## 6. Migration Verification

Run or verify:

```powershell
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

If migration already exists, do not create a duplicate. Instead verify:

- migration exists
- migration applies cleanly
- database update succeeds
- tables/constraints/indexes match expectations

If migration generation is explicitly part of the review and safe to perform, use:

```powershell
dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

Do not create duplicate migrations solely for verification.

Record:

- command
- exit status
- key output
- environment notes

Fail if migration cannot apply due to implementation error.

## 7. Transaction-Safe Event Append Verification

Verify event append uses a transaction-safe sequence strategy.

Preferred:

- `WorkflowExecutions.NextSequence`
- `SELECT ... FOR UPDATE` or equivalent row lock
- transaction wraps sequence read, increment, and event insert

Acceptable fallback:

- unique `(WorkflowId, Sequence)` plus retry-on-conflict

Verify:

- sequence is monotonic per workflow
- sequence is unique per workflow
- sequence remains stable after persistence
- replay ordering uses `Sequence` ascending

Tests must prove:

- append persists event
- `NextSequence` increments
- unique `(WorkflowId, Sequence)` is enforced
- replay returns events ordered by sequence

Flag any implementation that calculates sequence by `MAX(sequence) + 1` without locking or retry protection.

## 8. Protocol Version Verification

Verify Sprint 2 uses:

```text
protocolVersion: "0.2"
```

Verify implementation does not claim:

```text
protocol 1.0 complete
```

Verify bad protocol versions return:

```text
error payload code = bad_protocol_version
```

## 9. WebSocket Authentication Verification

Endpoint:

```text
/ws/events
```

Verify:

- `X-Chroma-Dev-Token` is required
- missing token rejected
- invalid token rejected
- valid token accepted
- query-string `devToken` is smoke-test only if still supported
- localhost/LAN binding behavior from Sprint 1 is preserved
- no broad CORS is added

Auth failure should reject before upgrade when possible.

## 10. Durable vs. Non-Durable Event Verification

This is critical. Do not let this blur.

Durable Sprint 2 events:

Only these should be inserted into `ExecutionEvents`:

- `workflow.started`
- `workflow.status` emitted during `workflow.start`

Non-durable Sprint 2 protocol responses:

These must use:

```text
sequence: null
```

and must not be inserted into `ExecutionEvents`:

- `connection.ready`
- `ack.updated` status
- `ack.noop` status
- resume-current `workflow.status`
- recoverable error envelopes

Verify tests prove:

- `workflow.start` creates exactly expected durable events
- `connection.ready` does not create `ExecutionEvents` row
- `event.ack` responses do not create `ExecutionEvents` rows
- resume-current status does not create `ExecutionEvents` row
- recoverable error envelopes do not create `ExecutionEvents` rows
- `session.resume` replays persisted durable events only

Fail if non-durable protocol responses are persisted in durable storage unless there is explicit documentation stating the rationale and tests proving the behavior.

## 11. `workflow.start` Verification

Verify inbound requirements:

- `workspaceId` required
- `workflowId` required
- `sessionId` required
- `messageId` required
- `protocolVersion = 0.2`
- `name = workflow.start`
- `timestamp` required
- `payload` must be object

Verify missing `workflowId` returns:

```text
error code = missing_required_field
```

Verify this decision is documented in:

- `docs/API_CONTRACT.md`
- `docs/PHASE_02_SPRINT_02_REPORT.md`
- ADR if present

Required behavior:

- creates `Workspaces` row if absent and documents this behavior
- creates or reuses supplied `WorkflowExecution`
- creates or reuses supplied `WorkflowSession`
- appends durable `workflow.started`
- appends durable `workflow.status`
- emits persisted events to client
- uses transaction-safe batch

Verify idempotency:

- same workflow/name/idempotencyKey + same payload hash returns existing durable started/status events
- same workflow/name/idempotencyKey + different payload hash returns `idempotency_conflict`
- missing idempotencyKey is allowed but receives no duplicate protection

Verify `PayloadHash` is SHA-256 over canonicalized payload JSON.

## 12. `session.resume` Verification

Verify inbound payload:

```json
{
	"lastSeenSequence": 3
}
```

Required behavior:

- `lastSeenSequence >= 0`
- `0` replays all events
- middle sequence replays events where `sequence > lastSeenSequence`
- latest sequence replays no prior events and returns documented non-durable status
- future sequence returns `future_sequence`
- replayed events keep original names, messageIds, timestamps, sequences, and payloads
- no replay marker is persisted

Verify replay order:

```text
ascending Sequence
```

Fail if resume creates duplicate durable events.

## 13. `event.ack` Verification

Verify inbound payload:

```json
{
	"lastSeenSequence": 5
}
```

Required behavior:

- `lastSeenSequence >= 0`
- `lastSeenSequence > max workflow sequence` returns `future_ack`
- `lastSeenSequence <= current ACK` is no-op
- `lastSeenSequence > current ACK` updates ACK state
- `ack.updated` and `ack.noop` responses are non-durable with `sequence: null`
- ACK does not create durable event unless explicitly justified and documented

Verify ACK does not mean:

- approval
- permission
- file edit execution
- terminal execution
- tool execution
- MCP execution

Fail if ACK unlocks execution behavior.

## 14. Error Envelope Verification

All recoverable protocol errors must return:

```json
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
```

Required error codes:

- `invalid_json`
- `bad_protocol_version`
- `unknown_message_name`
- `missing_required_field`
- `invalid_id`
- `workflow_not_found`
- `session_not_found`
- `idempotency_conflict`
- `future_ack`
- `future_sequence`
- `workflow_cancelled`
- `unauthorized`
- `internal_error`

Verify errors do not include:

- tokens
- connection strings
- passwords
- raw prompts
- provider keys
- raw stack traces
- full payload bodies
- raw upstream responses

## 15. Service Boundary Verification

Verify these exist and are used:

- `IEventStore`
- `PostgresEventStore`
- `IAcknowledgementStore`
- `PostgresAcknowledgementStore`
- `IWorkflowProtocolService`
- `WorkflowProtocolService`
- `IProtocolMessageValidator`
- `ProtocolMessageValidator`
- `ProtocolErrorFactory`
- `ProtocolActivitySource`

Verify old Sprint 1 protocol types are either:

- migrated cleanly
- left as compatibility-only with no conflicting runtime path
- documented as deprecated

Fail if duplicate protocol stacks conflict.

## 16. Observability and Security Verification

Verify structured logs exist for:

- `websocket.connection.accepted`
- `websocket.connection.rejected`
- `protocol.message.received`
- `protocol.message.rejected`
- `workflow.started`
- `event.appended`
- `event.replayed`
- `event.ack.updated`
- `event.ack.noop`

Verify ActivitySource exists:

```text
ChromaAgentics.Backend.Protocol
```

Verify spans exist around:

- `workflow.start`
- `event.append`
- `session.resume`
- `event.replay`
- `event.ack`

Verify logs include metadata only:

- workflowId
- sessionId
- sequence
- message name
- correlationId
- result status
- error code

Verify logs do not include:

- payload bodies
- tokens
- prompts
- provider keys
- full DB connection strings
- passwords
- raw upstream responses

## 17. JSON Schema Verification

Verify these schema files exist:

- `docs/schemas/protocol/v0.2/envelope.schema.json`
- `docs/schemas/protocol/v0.2/workflow-start.schema.json`
- `docs/schemas/protocol/v0.2/session-resume.schema.json`
- `docs/schemas/protocol/v0.2/event-ack.schema.json`
- `docs/schemas/protocol/v0.2/error-envelope.schema.json`

Verify schemas match implementation:

- `protocolVersion = 0.2`
- `workflow.start` requires `workflowId`
- `event.ack` payload requires `lastSeenSequence`
- `session.resume` payload requires `lastSeenSequence`
- error envelope shape matches implementation
- sequence nullable where appropriate

Flag schema drift.

## 18. Automated Test Verification

Run:

```powershell
dotnet test backend/ChromaAgentics.Backend.sln
```

Verify test groups exist or are clearly represented:

- unit tests
- integration tests
- WebSocket contract tests
- security/redaction tests

Required unit coverage:

- protocol envelope validation
- invalid JSON
- bad protocol version
- unknown message name
- missing IDs
- invalid UUIDs
- error factory safety
- payload hash/idempotency conflict logic
- ACK decision logic
- redaction behavior

Required integration coverage:

- migration applies
- append persists event
- `NextSequence` increments
- unique `(WorkflowId, Sequence)` enforced
- replay returns only events after sequence
- duplicate idempotency does not create duplicate event
- changed payload conflicts
- ACK cumulatively updates
- future ACK errors

Required WebSocket contract coverage:

- auth failure
- invalid JSON
- unknown message
- missing IDs
- bad protocol version
- `workflow.start` success
- resume from 0
- resume from middle sequence
- resume from latest sequence
- duplicate idempotency key
- changed-payload conflict
- duplicate/lower ACK no-op
- future ACK error

Verify Testcontainers PostgreSQL is used or fallback is documented.

Fail if tests require live Ollama.

## 19. Manual Smoke Test Verification

If safe, run:

```powershell
docker compose up --build
```

Then run:

```powershell
curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies
```

Run a WebSocket smoke test using the documented tool/script.

Smoke flow:

1. connect with valid `X-Chroma-Dev-Token`
2. receive non-durable `connection.ready` with `protocolVersion 0.2` and `sequence null`
3. send `workflow.start` with client-supplied `workflowId`
4. receive persisted `workflow.started` and `workflow.status`
5. send `event.ack`
6. receive non-durable `ack.updated` or documented response
7. disconnect
8. reconnect
9. send `session.resume`
10. verify missed events replay in ascending sequence order
11. verify latest resume returns no durable replay and documented non-durable status

Record exact commands or scripts used.

## 20. Documentation Verification

`docs/API_CONTRACT.md` must document:

- protocol version `0.2`
- implemented inbound messages
- implemented outbound messages
- envelope shape
- `workflow.start` example
- client-supplied `workflowId` requirement
- `session.resume` example
- `event.ack` example
- error envelope shape
- idempotency rules
- ACK rules
- resume/replay rules
- durable vs non-durable protocol responses
- auth behavior
- schema file locations
- planned-only features

`docs/GETTING_STARTED_BACKEND.md` must document:

- restore/build/test commands
- migration commands
- docker compose setup
- health curl examples
- WebSocket protocol `0.2` smoke test
- bad token troubleshooting
- migration troubleshooting
- replay mismatch troubleshooting

`docs/PHASE_02_SPRINT_02_REPORT.md` must include:

- Sprint 1 gate result
- final scope
- package versions
- database migration summary
- schema summary
- files changed
- commands run
- validation results
- test results
- WebSocket behavior
- replay behavior
- ACK behavior
- idempotency behavior
- durable vs non-durable behavior
- known gaps
- risks
- stretch work status
- next sprint recommendation

Docs must clearly mark these as planned only:

- protocol `1.0`
- `workflow.cancel` unless implemented as stretch
- approval execution
- full extension bridge
- Microsoft Agent Framework workflows
- Ollama chat
- model streaming
- model discovery
- pgvector
- RAG
- tool execution
- file edit execution
- terminal execution
- MCP execution
- Next.js dashboard
- LangGraph
- n8n

## 21. Command Validation Summary

Required commands:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

If migration generation is explicitly part of the review and safe to perform:

```powershell
dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

Do not create duplicate migrations solely for verification.

For every command, record:

- command
- pass/fail
- exit code if visible
- key output lines
- environment notes

## 22. Final Report Format

Output your report exactly in this structure:

```markdown
# Phase 2 Sprint 2 Verification & Validation Report

## Verdict

PASS / PASS WITH ISSUES / FAIL

## Executive Summary

Brief result summary.

## Validation Matrix

| Area                      |    Result | Evidence | Issues |
| ------------------------- | --------: | -------- | ------ |
| Sprint 1 gate             | PASS/FAIL | ...      | ...    |
| Canonical files           | PASS/FAIL | ...      | ...    |
| Scope discipline          | PASS/FAIL | ...      | ...    |
| Packages                  | PASS/FAIL | ...      | ...    |
| EF schema/migration       | PASS/FAIL | ...      | ...    |
| Constraints/indexes       | PASS/FAIL | ...      | ...    |
| Event sequencing          | PASS/FAIL | ...      | ...    |
| WebSocket auth            | PASS/FAIL | ...      | ...    |
| workflow.start            | PASS/FAIL | ...      | ...    |
| session.resume            | PASS/FAIL | ...      | ...    |
| event.ack                 | PASS/FAIL | ...      | ...    |
| Idempotency               | PASS/FAIL | ...      | ...    |
| Durable/non-durable split | PASS/FAIL | ...      | ...    |
| Error envelopes           | PASS/FAIL | ...      | ...    |
| Observability/security    | PASS/FAIL | ...      | ...    |
| JSON schemas              | PASS/FAIL | ...      | ...    |
| Tests                     | PASS/FAIL | ...      | ...    |
| Docs                      | PASS/FAIL | ...      | ...    |

## Commands Run

List exact commands, pass/fail, and key output.

## API and WebSocket Results

Summarize health, auth, workflow.start, resume, and ACK observations.

## Database/Migration Results

Summarize migration, tables, constraints, indexes, and event persistence.

## Security Findings

List secret/logging/auth/CORS/binding issues.

## Missing or Failed Requirements

Use severity:

- Critical
- High
- Medium
- Low

## Required Fixes Before Approval

Concrete required changes.

## Deferred Work Confirmed

List correctly deferred features.

## Final Recommendation

Approve, approve with fixes, or reject.
```

## 23. Scoring Rules

Use this scoring:

### PASS

All required implementation, tests, docs, migrations, protocol behavior, and validations pass.

### PASS WITH ISSUES

Core durable protocol works, but minor docs/test/report gaps remain. No security issues. No scope creep. No broken build.

### FAIL

Any of the following:

- backend does not build
- tests fail without justified environmental cause
- migration does not apply
- required tables missing
- sequence assignment unsafe
- WebSocket auth broken
- `workflow.start` broken
- `session.resume` broken
- `event.ack` broken
- idempotency broken
- durable/non-durable split wrong
- secrets exposed
- LAN exposed by default
- backend executes file/terminal/MCP/approval actions
- fake protocol `1.0` claim
- fake MAF/Ollama/RAG/approval execution claim
- required docs missing

Critical failures override everything. No amount of pretty markdown rescues a broken protocol.
