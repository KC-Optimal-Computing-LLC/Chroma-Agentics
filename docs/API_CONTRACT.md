# Chroma Agentics Backend API Contract

This document describes the Sprint 1 backend contract implemented under `backend/`.
It is intentionally small: health endpoints, local configuration behavior, and a
minimal authenticated WebSocket event stream.

## Implemented HTTP Endpoints

### `GET /health/live`

Process liveness only. This endpoint does not check PostgreSQL, Ollama, model
providers, the extension, or any future orchestration runtime.

Status: `200`

```json
{
	"status": "healthy",
	"service": "chroma-agentics-backend",
	"timestampUtc": "2026-05-09T20:00:00.0000000Z"
}
```

### `GET /health/ready`

Readiness is based on required configured dependencies. The response always
includes the dependency list so callers can see optional degraded dependencies
even when the backend is ready.

Status: `200` when all required dependencies are healthy. Status: `503` when any
required dependency is `unhealthy` or `not_configured`.

```json
{
	"status": "healthy",
	"service": "chroma-agentics-backend",
	"timestampUtc": "2026-05-09T20:00:00.0000000Z",
	"dependencies": [
		{
			"name": "postgresql",
			"status": "healthy",
			"required": true,
			"checkedAtUtc": "2026-05-09T20:00:00.0000000Z",
			"error": null
		},
		{
			"name": "ollama",
			"status": "unhealthy",
			"required": false,
			"checkedAtUtc": "2026-05-09T20:00:00.0000000Z",
			"error": "Ollama is unavailable."
		}
	]
}
```

### `GET /health/dependencies`

Detailed dependency status. This endpoint returns `200` even when dependencies
are unhealthy because it is a diagnostic endpoint.

Top-level `status` values:

- `healthy`: all dependencies are healthy
- `degraded`: required dependencies are healthy, but at least one optional dependency is unavailable
- `unhealthy`: at least one required dependency is unavailable or not configured

Dependency `status` values are `healthy`, `unhealthy`, or `not_configured`.
Errors are safe summaries and must not include connection strings, tokens,
prompts, passwords, or raw secret values.

## Implemented WebSocket Stream

Endpoint: `/ws/events`

Authentication:

- Preferred: `X-Chroma-Dev-Token` header
- Smoke-test only: `devToken` query string

Missing or invalid tokens are rejected with HTTP `401` before WebSocket upgrade
when possible. Query-string tokens are less secure because they can appear in
logs, shell history, browser history, and proxy logs. Do not use query-string
tokens for normal development workflows.

On a valid connection, the backend emits one `workflow.status` event:

```json
{
	"protocolVersion": "0.1",
	"messageId": "uuid",
	"workspaceId": null,
	"workflowId": "uuid",
	"sessionId": "uuid",
	"sequence": 1,
	"name": "workflow.status",
	"correlationId": null,
	"idempotencyKey": null,
	"timestamp": "2026-05-09T20:00:00.0000000Z",
	"payload": {
		"status": "connected",
		"detail": "Event stream connected."
	}
}
```

The endpoint accepts `sessionId` and `workflowId` query parameters when they are
valid UUIDs. If omitted, the backend generates UUIDs. The endpoint supports
client-initiated graceful close.

## Contract Models

Implemented model files:

- `ProtocolEnvelope<TPayload>`
- `ProtocolEventNames`
- `WorkflowStatusPayload`
- `ErrorPayload`
- `ApprovalRequestPayload` and `ApprovalDecisionPayload` as future-facing contract types only

## Security And Boundary Notes

- Backend defaults to localhost binding.
- LAN binding requires `CHROMA_ALLOW_LAN_BINDING=true`.
- Broad CORS is not enabled.
- Startup logging is structured and redacted.
- The backend must not execute file edits, terminal commands, MCP tools, or Roo approval bypasses.
- Approval request and decision payloads are contract placeholders only.

## Planned, Not Implemented

The following are planned for later phases and are not implemented in Sprint 1:

- Replay and ACK semantics
- Real approval execution
- Extension bridge UI/connection flow
- Microsoft Agent Framework workflows
- Ollama chat, streaming model responses, or model discovery
- PostgreSQL durable workflow schema or EF Core migrations
- pgvector, memory, embeddings, or RAG
- File edit execution, terminal execution, or MCP execution from the backend
