# Chroma Agentics Backend API Contract

This document describes the implemented Phase 2 Sprint 2 backend contract.
Protocol `0.2` is a narrow durable protocol slice for WebSocket workflow shells,
event replay, and cumulative ACK tracking. It is not protocol `1.0`.

## HTTP Endpoints

### `GET /health/live`

Process liveness only. Returns `200` with a `healthy` service response when the
process is running.

### `GET /health/ready`

Readiness checks required dependencies. PostgreSQL is required by default;
Ollama is optional unless `CHROMA_REQUIRE_OLLAMA=true`.

Returns `200` when all required dependencies are healthy and `503` when any
required dependency is unavailable or not configured.

### `GET /health/dependencies`

Diagnostic dependency status. Returns `200` even when dependencies are unhealthy.
Errors are safe summaries and must not include connection strings, tokens,
prompts, passwords, or raw secret values.

## WebSocket Endpoint

Endpoint: `/ws/events`

Authentication:

- Preferred: `X-Chroma-Dev-Token` header
- Smoke-test only: `devToken` query string

Missing or invalid tokens are rejected with HTTP `401` before WebSocket upgrade
when possible. Query-string tokens are documented only for local smoke testing.

On successful connection the backend emits one non-durable `connection.ready`
envelope:

```json
{
	"protocolVersion": "0.2",
	"messageId": "uuid",
	"workspaceId": null,
	"workflowId": null,
	"sessionId": null,
	"sequence": null,
	"name": "connection.ready",
	"correlationId": null,
	"idempotencyKey": null,
	"timestamp": "2026-05-09T20:00:00Z",
	"payload": {
		"status": "ready",
		"protocolVersion": "0.2"
	}
}
```

## Envelope Shape

Inbound messages use:

```json
{
	"protocolVersion": "0.2",
	"messageId": "uuid",
	"workspaceId": "uuid",
	"workflowId": "uuid",
	"sessionId": "uuid",
	"sequence": null,
	"name": "workflow.start",
	"correlationId": "uuid-or-null",
	"idempotencyKey": "string-or-null",
	"timestamp": "ISO-8601",
	"payload": {}
}
```

Persisted outbound events include assigned `sequence` values. Non-durable status
or error responses use `sequence: null`.

Implemented inbound messages:

- `workflow.start`
- `session.resume`
- `event.ack`

Implemented outbound messages:

- `connection.ready`
- `workflow.started`
- `workflow.status`
- `error`

## `workflow.start`

Sprint 2 requires `workflowId`; backend-generated workflow IDs are deferred.

Example:

```json
{
	"protocolVersion": "0.2",
	"messageId": "90fd864c-f7f2-4f47-86cf-681773aa6a97",
	"workspaceId": "40761f96-91fa-4990-9b2e-e98e72f6b315",
	"workflowId": "c90f3997-61aa-4944-a191-ecc01f920b64",
	"sessionId": "5eae23a6-4287-45c5-94f9-c9ec558b55d0",
	"sequence": null,
	"name": "workflow.start",
	"correlationId": null,
	"idempotencyKey": "manual-smoke-start",
	"timestamp": "2026-05-09T20:00:00Z",
	"payload": {
		"title": "Smoke test workflow",
		"mode": "orchestrator",
		"source": "manual-smoke-test"
	}
}
```

Behavior:

- Creates the workspace if it does not exist.
- Creates or reuses the supplied workflow execution and session.
- Persists durable `workflow.started` and `workflow.status` events atomically
  with workflow/session creation or reuse.
- Assigns monotonic per-workflow sequences using `WorkflowExecutions.NextSequence`
  inside a PostgreSQL transaction with a row lock.

Idempotency:

- Same workflow/name/idempotency key and same payload hash returns the previously
  persisted `workflow.started` and `workflow.status` events.
- Same workflow/name/idempotency key and different payload hash returns
  `idempotency_conflict`.
- Missing idempotency key is allowed, with no duplicate protection.
- Payload hashes are SHA-256 over canonical JSON: object properties are sorted
  lexicographically, array order is preserved, and primitive values are written
  in normalized JSON form.

## `session.resume`

Example:

```json
{
	"protocolVersion": "0.2",
	"messageId": "f862d1be-ef1d-4987-8ecc-ee453ec94ffb",
	"workspaceId": "40761f96-91fa-4990-9b2e-e98e72f6b315",
	"workflowId": "c90f3997-61aa-4944-a191-ecc01f920b64",
	"sessionId": "5eae23a6-4287-45c5-94f9-c9ec558b55d0",
	"sequence": null,
	"name": "session.resume",
	"correlationId": null,
	"idempotencyKey": null,
	"timestamp": "2026-05-09T20:01:00Z",
	"payload": {
		"lastSeenSequence": 1
	}
}
```

Replay rules:

- `lastSeenSequence = 0`: replay all persisted workflow events.
- Middle sequence: replay events where `sequence > lastSeenSequence`.
- Latest sequence: emit non-durable `workflow.status` with `status:
"resume.current"`.
- Future sequence: return `future_sequence`.
- Replayed events keep original names, message IDs, timestamps, payloads, and
  sequence numbers.
- Replay never creates new `ExecutionEvents` rows.

## `event.ack`

Example:

```json
{
	"protocolVersion": "0.2",
	"messageId": "2a5a4a82-e588-4793-a94a-145192d623d2",
	"workspaceId": "40761f96-91fa-4990-9b2e-e98e72f6b315",
	"workflowId": "c90f3997-61aa-4944-a191-ecc01f920b64",
	"sessionId": "5eae23a6-4287-45c5-94f9-c9ec558b55d0",
	"sequence": null,
	"name": "event.ack",
	"correlationId": null,
	"idempotencyKey": null,
	"timestamp": "2026-05-09T20:02:00Z",
	"payload": {
		"lastSeenSequence": 2
	}
}
```

ACK rules:

- ACK is cumulative receipt and processing of protocol events through
  `lastSeenSequence`.
- `lastSeenSequence > max workflow sequence` returns `future_ack`.
- Lower or duplicate ACK is a no-op and returns non-durable `workflow.status` with
  `status: "ack.noop"`.
- Higher ACK updates `EventAcknowledgements` and returns non-durable
  `workflow.status` with `status: "ack.updated"`.
- ACK is not approval, permission, file edit execution, terminal execution, tool
  execution, or MCP execution.

## Error Envelope

Recoverable protocol errors return safe `error` envelopes:

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
		"code": "future_ack",
		"message": "lastSeenSequence is ahead of the latest persisted workflow event.",
		"retryable": false
	}
}
```

Implemented error codes:

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

Errors and logs must not include tokens, connection strings, passwords, raw
prompts, provider keys, raw stack traces in responses, full payload bodies, or
raw upstream responses.

## Schema Artifacts

Starter JSON Schemas live under:

```text
docs/schemas/protocol/v0.2/
```

Files:

- `envelope.schema.json`
- `workflow-start.schema.json`
- `session-resume.schema.json`
- `event-ack.schema.json`
- `error-envelope.schema.json`

## Sprint 3 Extension Bridge Client

The VS Code extension bridge is the first protocol `0.2` client. It does not
change the backend wire contract.

Extension bridge behavior:

- Backend settings use `chromaAgentics.backend.*`.
- The development token is stored only in VS Code SecretStorage.
- WebSocket auth uses the `X-Chroma-Dev-Token` header.
- The bridge does not use query-string token auth.
- `workflow.start` uses client-generated `workspaceId`, `workflowId`,
  `sessionId`, `messageId`, `correlationId`, `idempotencyKey`, and timestamp.
- Durable backend events are ACKed only after processing or intentional duplicate
  replay suppression.
- Reconnect uses memory-only `lastSeenSequence` and sends `session.resume`.
- Duplicate replay display is suppressed using `workflowId + sequence +
messageId`.
- Output is limited to safe metadata: backend status, workflow ID, session ID,
  event name, sequence, error code, and safe summary.

The bridge does not execute approvals, file edits, terminal commands, MCP/tool
actions, model calls, or orchestration workflows.

## Planned, Not Implemented

The following remain planned-only and are not implemented in Sprint 2 or the
Sprint 3 extension bridge:

- Protocol `1.0`
- `workflow.cancel`
- Approval execution
- Full extension bridge UI beyond the Sprint 3 status/output bridge
- Microsoft Agent Framework workflows
- Ollama chat, model streaming, and model discovery
- pgvector and RAG
- Tool execution
- File edit execution
- Terminal execution
- MCP execution
- Next.js dashboard
- LangGraph
- n8n
