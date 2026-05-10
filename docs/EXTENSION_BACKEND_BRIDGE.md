# Extension Backend Bridge

Phase 2 Sprint 3 adds a narrow VS Code extension bridge for the Sprint 2 backend
protocol `0.2`. The bridge is a smoke and integration surface only. It does not
execute approvals, file edits, terminal commands, MCP actions, tools, model
calls, or orchestration workflows.

## Settings

The bridge uses the `chromaAgentics.backend.*` namespace:

| Setting                                           |                 Default | Purpose                                       |
| ------------------------------------------------- | ----------------------: | --------------------------------------------- |
| `chromaAgentics.backend.enabled`                  |                 `false` | Allows automatic bridge connection when true. |
| `chromaAgentics.backend.url`                      | `http://localhost:5127` | Backend base URL.                             |
| `chromaAgentics.backend.connectionTimeoutMs`      |                  `5000` | Health and WebSocket connection timeout.      |
| `chromaAgentics.backend.reconnect.enabled`        |                  `true` | Reconnect after unexpected WebSocket close.   |
| `chromaAgentics.backend.reconnect.maxAttempts`    |                     `5` | Maximum reconnect attempts.                   |
| `chromaAgentics.backend.reconnect.initialDelayMs` |                  `1000` | Initial reconnect delay.                      |

When `enabled=false`, activation registers commands and creates the status bar,
but does not call backend health endpoints, poll health, or open a WebSocket.
Explicit user-invoked backend commands may still run.

## Commands

| Command ID                                  | Command Palette Title           |
| ------------------------------------------- | ------------------------------- |
| `chromaAgentics.backend.setToken`           | Set/Replace Backend Token       |
| `chromaAgentics.backend.clearToken`         | Clear Backend Token             |
| `chromaAgentics.backend.health`             | Test Backend Health             |
| `chromaAgentics.backend.connect`            | Connect Backend Event Stream    |
| `chromaAgentics.backend.startSmokeWorkflow` | Start Backend Smoke Workflow    |
| `chromaAgentics.backend.disconnect`         | Disconnect Backend Event Stream |

The dev token is stored only in VS Code SecretStorage under
`chromaAgentics.backend.devToken`. The token is never written to the output
channel, status bar, logs, or protocol payloads.

## Smoke Flow

1. Start the backend and PostgreSQL.
2. Set `chromaAgentics.backend.url` to `http://localhost:5127`.
3. Run `Set/Replace Backend Token` and enter the value of
   `CHROMA_DEV_AUTH_TOKEN`.
4. Run `Test Backend Health`.
5. Run `Connect Backend Event Stream`.
6. Run `Start Backend Smoke Workflow`.
7. Confirm the output channel shows safe metadata for `connection.ready`,
   `workflow.started`, `workflow.status`, ACK status, and resume behavior.
8. Run `Disconnect Backend Event Stream`, then reconnect to verify
   `session.resume`.

Expected protocol behavior:

- `connection.ready` is non-durable and has `sequence: null`.
- `workflow.start` is sent with client-generated workspace, workflow, session,
  message, correlation, and idempotency IDs.
- `workflow.started` and start `workflow.status` are durable and ACKed after
  processing.
- Reconnect sends `session.resume` with the in-memory `lastSeenSequence`.
- Duplicate replayed durable events are ignored for display and still ACKed.

## Safe Output

The output channel may include only:

- backend status
- workflow ID
- session ID
- event name
- sequence
- error code
- safe summary

The output channel must not include tokens, full payload bodies, prompts,
provider keys, connection strings, terminal command text, tool payloads, file
proposal payloads, or raw backend stack traces.

## Failure Modes

The bridge handles these failures without crashing:

- backend offline
- unhealthy health response
- missing token
- invalid token
- WebSocket close
- malformed backend event
- protocol version mismatch
- `future_sequence`
- `future_ack`
- `idempotency_conflict`

Failures update the status bar and output channel with safe summaries only.

## Scope Boundary

The Sprint 3 bridge is intentionally isolated under
`src/services/backend-bridge/`. Automated tests statically check that bridge
source does not use file edit APIs, terminal execution APIs, MCP managers,
approval paths, tool execution paths, dashboards, or webviews.
