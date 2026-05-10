# Agent Instruction: Phase 2 Sprint 3 - Extension Bridge + End-to-End Event Streaming

You are the Phase 2 Sprint 3 implementation agent for Chroma Agentics.

## Mission

Build the smallest safe VS Code extension bridge that proves the Roo-derived
extension can connect to the Sprint 2 backend and demonstrate protocol `0.2`
event streaming.

Sprint 3 must prove:

- Backend health check works from the extension.
- Extension connects to `/ws/events`.
- Extension authenticates using `X-Chroma-Dev-Token`.
- Extension sends `workflow.start`.
- Extension receives `workflow.started` and `workflow.status`.
- Extension sends `event.ack` after processing durable events.
- Extension reconnects and sends `session.resume`.
- Extension suppresses duplicate replay display.
- Extension shows safe status/output feedback.

## Scope Rules

Do not implement Orchestrator logic, Microsoft Agent Framework workflows, Ollama
chat, model streaming, model discovery, approval execution, file edits, terminal
commands, MCP/tool execution, RAG, pgvector, dashboards, Next.js, LangGraph, n8n,
cloud provider adapters, production auth, multi-user authorization, or a Phase 2
completion claim during implementation.

README status updates are out of scope for Sprint 3 implementation. The Sprint 3
report may recommend Phase 2 completion after validation, but must not apply it.

## Sprint 2 Gate

Before extension bridge work, run:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

Verify Sprint 2 report/acceptance exists, `/ws/events` requires
`X-Chroma-Dev-Token`, protocol version is `0.2`, `workflow.start`, `event.ack`,
and `session.resume` work, and durable/non-durable behavior is documented.

## Extension Bridge Requirements

Register bridge commands through the existing extension activation path. Do not
create a second activation system. Command registration and status bar creation
are allowed on activation. Network calls, health polling, and WebSocket
connections are prohibited unless `chromaAgentics.backend.enabled=true` or the
user explicitly invokes a backend command.

Settings:

- `chromaAgentics.backend.enabled=false`
- `chromaAgentics.backend.url=http://localhost:5127`
- `chromaAgentics.backend.connectionTimeoutMs=5000`
- `chromaAgentics.backend.reconnect.enabled=true`
- `chromaAgentics.backend.reconnect.maxAttempts=5`
- `chromaAgentics.backend.reconnect.initialDelayMs=1000`

Commands:

- `chromaAgentics.backend.setToken`
- `chromaAgentics.backend.clearToken`
- `chromaAgentics.backend.health`
- `chromaAgentics.backend.connect`
- `chromaAgentics.backend.startSmokeWorkflow`
- `chromaAgentics.backend.disconnect`

Store the backend dev token only in VS Code SecretStorage. Never log or display
the token.

## Protocol Behavior

Extension-created envelopes must match `docs/schemas/protocol/v0.2/*.schema.json`.
Use client-generated `workspaceId`, `workflowId`, `sessionId`, `messageId`,
`correlationId`, `idempotencyKey`, and timestamp for `workflow.start`.

Session state is memory-only in Sprint 3:

- `workspaceId`
- `workflowId`
- `sessionId`
- `lastSeenSequence`
- `processedMessageIds`
- `connectionState`
- `lastErrorCode`

Track processed durable events by `workflowId + sequence + messageId`. Do not
display replayed duplicates twice. Send ACK only after the event is processed or
intentionally ignored as a duplicate.

Use only a VS Code status bar item and output channel. Do not build a dashboard
or redesign the webview.

## Security

Bridge output may log only safe metadata: backend status, workflow ID, session
ID, event name, sequence, error code, and safe summary.

Never log tokens, full payload bodies, prompts, provider keys, connection
strings, terminal command text, tool payloads, or file proposal payloads.

Bridge code must not call file edit APIs, terminal execution APIs, MCP execution
APIs, approval execution paths, tool execution paths, dashboards, or webviews.

## Required Tests

Add extension tests for config defaults, activation disabled behavior,
SecretStorage token lifecycle, missing token behavior, URL/header construction,
protocol envelope creation, Ajv schema validation, inbound parsing for
`connection.ready`, `workflow.started`, `workflow.status`, and `error`, ACK
sequence tracking, resume payload generation, duplicate replay suppression, safe
output formatting, failure handling, and static scope boundary checks.

Run:

```powershell
pnpm --filter roo-cline check-types
pnpm --filter roo-cline test
pnpm --filter roo-cline lint
pnpm --filter roo-cline bundle
```

Also run backend regression:

```powershell
dotnet test backend/ChromaAgentics.Backend.sln
```

## Documentation

Create:

- `docs/EXTENSION_BACKEND_BRIDGE.md`
- `docs/PHASE_02_SPRINT_03_REPORT.md`

Update:

- `docs/API_CONTRACT.md`
- `docs/GETTING_STARTED_BACKEND.md`

The Sprint 3 report must state whether README status changed, confirm command
registration used the existing activation convention, confirm no second
activation system exists, confirm disabled mode performs no auto-connect, health
polling, or WebSocket connection, and confirm schema/parsing tests.
