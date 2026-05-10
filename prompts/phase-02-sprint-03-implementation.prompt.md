# Implementation Prompt: Phase 2 Sprint 3 - Extension Bridge + End-to-End Event Streaming

You are working in the Chroma Agentics repository.

Implement Phase 2 Sprint 3: Extension Bridge + End-to-End Event Streaming.

This sprint adds the smallest safe VS Code extension bridge proving the Roo-derived
extension can connect to the Sprint 2 backend and exercise protocol `0.2` event
streaming end to end.

Do not expand scope beyond Sprint 3. Do not implement orchestrator logic,
Microsoft Agent Framework workflows, Ollama chat, model streaming, model
discovery, approval execution, file edits, terminal execution, MCP/tool
execution, RAG, pgvector, dashboards, webview redesign, production auth, or
README phase-complete claims.

---

## Source of Truth

Use:

- `README.md`
- `ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md`
- `docs/API_CONTRACT.md`
- `docs/GETTING_STARTED_BACKEND.md`
- `docs/PHASE_02_SPRINT_02_REPORT.md`
- `agents/phase-02-sprint-03-extension-bridge.agent.md`
- existing Sprint 2 backend and extension implementation

For prompt artifacts in this repo, `.prompt.md` is the canonical naming
convention for Sprint 3 prompt files.

---

## Required First Step: Sprint 2 Gate

Before editing, verify Sprint 2 still passes.

Run:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

If safe, also verify local backend behavior:

```powershell
docker compose up -d --build backend
curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies
```

Confirm:

- Sprint 2 report exists.
- `/ws/events` requires `X-Chroma-Dev-Token`.
- protocol version is `0.2`.
- `workflow.start`, `event.ack`, and `session.resume` work.
- durable vs non-durable behavior is documented.

If Sprint 2 is broken, stop and report blockers unless the issue is clearly
environmental and documented.

---

## Sprint Goal

Implement the narrow extension/backend bridge slice only.

Required bridge behavior:

- backend health check from the extension
- WebSocket connection to `/ws/events`
- auth using `X-Chroma-Dev-Token`
- send `workflow.start`
- receive `workflow.started` and `workflow.status`
- send `event.ack` after processing durable events
- reconnect and send `session.resume`
- suppress duplicate replay display
- show safe status bar and output channel feedback

Do not add a second activation system.

---

## Extension Settings

Use the canonical namespace:

```text
chromaAgentics.backend.*
```

Required settings and defaults:

- `chromaAgentics.backend.enabled=false`
- `chromaAgentics.backend.url=http://localhost:5127`
- `chromaAgentics.backend.connectionTimeoutMs=5000`
- `chromaAgentics.backend.reconnect.enabled=true`
- `chromaAgentics.backend.reconnect.maxAttempts=5`
- `chromaAgentics.backend.reconnect.initialDelayMs=1000`

Match all setting descriptions in `src/package.nls.json`.

---

## Required Commands

Register these command IDs through the existing extension activation path:

- `chromaAgentics.backend.setToken`
- `chromaAgentics.backend.clearToken`
- `chromaAgentics.backend.health`
- `chromaAgentics.backend.connect`
- `chromaAgentics.backend.startSmokeWorkflow`
- `chromaAgentics.backend.disconnect`

Required behavior:

- `setToken` stores or replaces the backend token in SecretStorage only
- `clearToken` removes the token from SecretStorage
- `health` checks backend health endpoints
- `connect` opens the backend event stream
- `startSmokeWorkflow` sends `workflow.start`
- `disconnect` closes the bridge connection safely

---

## SecretStorage And Security

Store the backend dev token only in:

```text
ExtensionContext.secrets
```

Expected key:

```text
chromaAgentics.backend.devToken
```

Never:

- store the token in settings or config
- log the token
- show the token in the output channel
- send the token by query string

Output may include safe metadata only:

- backend status
- workflow ID
- session ID
- event name
- sequence
- error code
- safe summary

---

## Protocol Requirements

Use protocol version:

```text
0.2
```

Extension-created envelopes must include client-generated identifiers for:

- `workspaceId`
- `workflowId`
- `sessionId`
- `messageId`
- `correlationId`
- `idempotencyKey`
- `timestamp`

Use `crypto.randomUUID()` or an existing safe UUID helper.

Convert backend URLs as follows:

- `http -> ws`
- `https -> wss`
- append `/ws/events`

Session state remains memory-only in Sprint 3.

Track:

- `workspaceId`
- `workflowId`
- `sessionId`
- `lastSeenSequence`
- `processedMessageIds`
- `connectionState`
- `lastErrorCode`

ACK rules:

- ACK durable events only
- ACK after processing
- ACK uses the highest processed sequence
- ACK is not approval and does not trigger execution

---

## Test Requirements

Add or update tests for:

- config defaults
- disabled activation behavior
- SecretStorage token lifecycle
- missing token behavior
- health URL and WebSocket URL construction
- auth header construction
- `workflow.start`, `event.ack`, and `session.resume` envelope creation
- Ajv schema validation against `docs/schemas/protocol/v0.2/*.schema.json`
- parsing for `connection.ready`, `workflow.started`, `workflow.status`, and `error`
- ACK tracking
- resume payload generation
- duplicate replay suppression
- safe output formatting
- runtime failure handling
- static scope boundary checks

Run:

```powershell
pnpm --filter roo-cline check-types
pnpm --filter roo-cline test
pnpm --filter roo-cline lint
pnpm --filter roo-cline bundle
dotnet test backend/ChromaAgentics.Backend.sln
```

---

## Documentation Requirements

Create or update:

- `docs/EXTENSION_BACKEND_BRIDGE.md`
- `docs/PHASE_02_SPRINT_03_REPORT.md`
- `docs/API_CONTRACT.md`
- `docs/GETTING_STARTED_BACKEND.md`

Document:

- settings
- command IDs
- SecretStorage behavior
- health checks
- WebSocket connection flow
- smoke workflow
- ACK behavior
- reconnect/resume behavior
- duplicate suppression
- security boundaries
- known gaps
- no second activation system
- no auto-connect or polling while disabled
- README status unchanged during implementation

---

## Delivery Constraints

Keep the solution narrow, testable, and repo-consistent.

Do not claim Phase 2 completion during implementation. That decision belongs to
separate verification and validation after the extension bridge is proven.
