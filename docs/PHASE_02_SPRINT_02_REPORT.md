# Phase 2 Sprint 2 Report

## Sprint Goal

Implement Durable Event Store + Resume/Replay for the backend protocol layer:
PostgreSQL persistence, monotonic event sequencing, `workflow.start`,
`session.resume`, cumulative `event.ack`, idempotency, safe protocol errors,
structured logs, ActivitySource tracing, protocol `0.2` docs, schemas, and tests.

## Sprint 1 Gate Result

Passed before implementation.

- Backend solution exists.
- `.env.example`, `docker-compose.yml`, and
  `docs/PHASE_02_SPRINT_01_REPORT.md` exist.
- Health endpoints exist.
- `/ws/events` exists and requires dev-token auth.
- `dotnet restore`, `dotnet build`, `dotnet test`, and `docker compose config`
  all succeeded.

## Final Scope

Delivered only Sprint 2 protocol infrastructure. The backend persists workflow
shell events and ACK state but does not execute approvals, file edits, terminal
commands, MCP actions, tools, model calls, or orchestration workflows.

## Package Versions

- Target framework: `net8.0`
- `Npgsql` `8.0.9`
- `Microsoft.EntityFrameworkCore` `8.0.23`
- `Microsoft.EntityFrameworkCore.Design` `8.0.23`
- `Microsoft.EntityFrameworkCore.Relational` `8.0.23`
- `Npgsql.EntityFrameworkCore.PostgreSQL` `8.0.11`
- `Microsoft.AspNetCore.Mvc.Testing` `8.0.23`
- `Microsoft.NET.Test.Sdk` `17.14.1`
- `Testcontainers.PostgreSql` `4.11.0`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `2.8.2`
- `coverlet.collector` `6.0.4`
- `dotnet-ef` tool installed at `8.0.23`

`Microsoft.EntityFrameworkCore.Relational 8.0.23` is explicitly pinned to avoid
assembly resolution warnings from the requested EF Core `8.0.23` and Npgsql EF
provider `8.0.11` combination.

## Database Migration Summary

Migration:

```text
backend/src/ChromaAgentics.Backend/Persistence/Migrations/20260509215511_Sprint02ProtocolSupport.cs
```

Validated with:

```powershell
dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend --output-dir Persistence/Migrations
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

The database update applied migration `20260509215511_Sprint02ProtocolSupport`
and created the EF migrations history table plus Sprint 2 protocol-support
tables.

## Schema Summary

Tables:

- `Workspaces`
- `WorkflowExecutions`
- `WorkflowSessions`
- `ExecutionEvents`
- `EventAcknowledgements`

Key constraints/indexes:

- Unique `(WorkflowId, Sequence)`
- Unique `(WorkflowId, MessageId)`
- Filtered unique `(WorkflowId, Name, IdempotencyKey)` where
  `IdempotencyKey IS NOT NULL`
- Unique `(WorkflowId, SessionId)` for ACK state
- Indexes for workflow sequence lookup, sessions, workspace lookup,
  idempotency keys, and creation/update timestamps

Sequence strategy:

- Implemented `WorkflowExecutions.NextSequence`.
- `PostgresEventStore.AppendEventAsync` opens a transaction, locks the workflow
  row with `SELECT ... FOR UPDATE`, assigns `NextSequence`, increments it,
  inserts `ExecutionEvents`, and commits.
- `workflow.start` now uses one transaction for workspace/workflow/session
  creation or reuse, allocation of both start event sequences, insertion of
  `workflow.started` and start `workflow.status`, `NextSequence` update, and
  commit. A rollback test simulates failure after `workflow.started` is saved
  inside the transaction and proves no partial durable start remains.
- Idempotency payload hashes use SHA-256 over canonical JSON. Object properties
  are sorted lexicographically, array order is preserved, and primitive values
  are written in normalized JSON form before hashing.

## Files Changed

Created:

- `agents/phase-02-sprint-02-durable-event-store.agent.md`
- `backend/src/ChromaAgentics.Backend/Persistence/*`
- `backend/src/ChromaAgentics.Backend/Events/*`
- `backend/src/ChromaAgentics.Backend/Acknowledgements/*`
- `backend/src/ChromaAgentics.Backend/Protocol/*`
- `backend/src/ChromaAgentics.Backend/Observability/ProtocolActivitySource.cs`
- `backend/src/ChromaAgentics.Backend/Persistence/Migrations/*`
- `backend/tests/ChromaAgentics.Backend.Tests/Events/*`
- `backend/tests/ChromaAgentics.Backend.Tests/Persistence/*`
- `backend/tests/ChromaAgentics.Backend.Tests/Protocol/*`
- `backend/tests/ChromaAgentics.Backend.Tests/Streaming/EventStreamTests.cs`
- `docs/PHASE_02_SPRINT_02_REPORT.md`
- `docs/ADRs/ADR-0001-phase-2-protocol-support-tables.md`
- `docs/schemas/protocol/v0.2/*.schema.json`
- `prompts/phase-02-sprint-02-verification-validation.md`

Modified:

- `backend/src/ChromaAgentics.Backend/ChromaAgentics.Backend.csproj`
- `backend/src/ChromaAgentics.Backend/Program.cs`
- `backend/src/ChromaAgentics.Backend/Streaming/EventStreamEndpoint.cs`
- `backend/tests/ChromaAgentics.Backend.Tests/ChromaAgentics.Backend.Tests.csproj`
- `backend/tests/ChromaAgentics.Backend.Tests/TestBackendFactory.cs`
- `docs/API_CONTRACT.md`
- `docs/GETTING_STARTED_BACKEND.md`

Removed:

- Stale Sprint 1 `Contracts/ProtocolEnvelope.cs`
- Stale Sprint 1 `Contracts/ProtocolEventNames.cs`

Moved logically:

- Sprint 1 root `EventStreamTests.cs` was replaced by
  `backend/tests/ChromaAgentics.Backend.Tests/Streaming/EventStreamTests.cs`.

Canonical prompt note:

- `prompts/phase-02-sprint-02-verification-validation.prompt.md` remains as an
  IDE-friendly duplicate naming variant; the canonical V&V prompt path is
  `prompts/phase-02-sprint-02-verification-validation.md`.

## Commands Run

Gate and validation:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

Migration and smoke:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.23
dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend --output-dir Persistence/Migrations
docker compose up -d postgres
$env:CHROMA_DATABASE_CONNECTION_STRING='Host=localhost;Port=5432;Database=chroma_agentics;Username=chroma;Password=chroma_dev_password'
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
docker compose up -d --build backend
curl.exe -s -i http://localhost:5127/health/live
curl.exe -s -i http://localhost:5127/health/ready
curl.exe -s -i http://localhost:5127/health/dependencies
docker compose down
```

The WebSocket smoke script documented in
`docs/GETTING_STARTED_BACKEND.md` was also run against the compose backend.

## Validation Results

- Restore: succeeded.
- Build: succeeded with `0 Warning(s)` and `0 Error(s)`.
- Tests: `Passed! - Failed: 0, Passed: 46, Skipped: 0, Total: 46`.
- Docker Compose config: rendered valid `backend` and `postgres` services.
- Docker Compose build/start: backend image built and backend/postgres started.
- EF database update: migration applied successfully.

Health smoke:

- `/health/live`: HTTP `200`, `healthy`.
- `/health/ready`: HTTP `200`, required PostgreSQL `healthy`.
- `/health/dependencies`: HTTP `200`, PostgreSQL `healthy`, optional Ollama
  reported healthy on this workstation.

WebSocket smoke:

```text
ready=connection.ready::0.2
started=workflow.started:1 status=workflow.status:2
ack=ack.updated:1:
replayed=workflow.status:2
current=resume.current:
future=future_sequence:
duplicate=1,2
conflict=idempotency_conflict:
```

Smoke database check for the workflow showed exactly two durable events and one
ACK row after connection, ACK, resume-current, future-sequence error,
reordered-payload idempotency retry, and changed-payload conflict.

## Test Results

Automated coverage includes:

- Protocol envelope validation
- Bad protocol version
- Unknown message name
- Missing required IDs
- Invalid UUIDs
- Error envelope creation
- Secret redaction regression
- EF migration application
- Durable append and `NextSequence` increment
- Unique workflow sequence enforcement
- Replay query ordering
- Idempotency duplicate and conflict behavior
- ACK cumulative update and no-op behavior
- Future ACK error
- WebSocket auth failure
- WebSocket invalid JSON
- Atomic `workflow.start` commit and rollback behavior
- Canonical JSON hashing, including reordered nested objects and array order
- WebSocket `workflow.start`, resume, idempotency, ACK, future resume, and
  future ACK behavior
- Recoverable protocol errors returning non-durable envelopes without creating
  `ExecutionEvents`

## WebSocket Behavior

- `/ws/events` still requires `X-Chroma-Dev-Token`.
- Query-string `devToken` remains smoke-test only.
- Valid connections receive non-durable `connection.ready`.
- Inbound messages are validated as protocol `0.2` envelopes.
- Recoverable protocol errors return safe `error` envelopes.

## Replay Behavior

- `session.resume` replays persisted events using original event names, message
  IDs, timestamps, payloads, and sequence numbers.
- Replay returns only events where `Sequence > lastSeenSequence`.
- Latest resume returns non-durable `workflow.status` with `resume.current`.
- Future resume returns `future_sequence`.
- No synthetic replay event is persisted.

## ACK Behavior

- ACK state is stored in `EventAcknowledgements`.
- ACK is cumulative by workflow/session.
- Lower or duplicate ACK returns non-durable `ack.noop`.
- Higher ACK updates state and returns non-durable `ack.updated`.
- Future ACK returns `future_ack`.
- ACK does not imply approval or execution permission.

## Idempotency Behavior

- `workflow.start` accepts missing idempotency keys but provides no duplicate
  protection in that case.
- Same workflow/name/idempotency key and same payload hash returns existing
  `workflow.started` and `workflow.status` events.
- Same workflow/name/idempotency key and different payload hash returns
  `idempotency_conflict`.
- Reordered JSON object properties hash to the same payload hash; changed values
  hash differently.

## Security And Observability Notes

- Localhost-first binding and LAN opt-in validation remain intact.
- Broad CORS is not enabled.
- WebSocket auth remains enforced.
- Protocol logs use metadata only: workflow ID, session ID, sequence, message
  name, correlation ID, result, and error code.
- Error envelopes avoid tokens, connection strings, passwords, raw prompts, raw
  stack traces, full payload bodies, provider keys, and upstream responses.
- Added ActivitySource: `ChromaAgentics.Backend.Protocol`.
- Added spans around `workflow.start`, `event.append`, `session.resume`,
  `event.replay`, and `event.ack`.

## JSON Schema And Docs Updates

- `docs/API_CONTRACT.md` now documents protocol `0.2`.
- `docs/GETTING_STARTED_BACKEND.md` now documents EF migrations and protocol
  smoke behavior, including a runnable PowerShell WebSocket smoke script and
  replay/idempotency troubleshooting.
- Starter JSON Schemas were created under
  `docs/schemas/protocol/v0.2/`.
- ADR created for the Sprint 2 protocol-support table decision.

## Known Gaps

- No protocol `1.0`.
- No `workflow.cancel`.
- No approval execution.
- No full extension bridge UI.
- No Microsoft Agent Framework workflows.
- No Ollama chat, model streaming, or model discovery.
- No pgvector, embeddings, RAG ingestion, or RAG retrieval.
- No backend tool execution, file edit execution, terminal execution, or MCP
  execution.
- No Next.js dashboard.
- No LangGraph or n8n integration.
- Production auth and multi-user authorization remain future work.

## Risks

- Development token auth is local bootstrap only.
- Query-string token support exists for smoke testing and should not be used for
  normal workflows.
- Durable events can contain workflow metadata and should be treated as sensitive
  operational data.
- `workflow.start` requires client-supplied `workflowId` in Sprint 2 to keep the
  idempotency model minimal.

## Stretch Work Status

`workflow.cancel` was not implemented. Cancellation remains future stretch work.

## Next Sprint Recommendation

Add a small extension/backend bridge handshake that uses protocol `0.2` and
keeps approval, file edits, terminal commands, tools, and MCP execution inside
the Roo-derived extension boundary.
