# Getting Started With The Backend

This guide starts the Phase 2 Sprint 2 backend foundation. The backend is a
local-first ASP.NET Core minimal API under `backend/` with PostgreSQL-backed
protocol event durability.

## Prerequisites

- .NET SDK capable of building `net8.0`
- ASP.NET Core runtime 8.0, or a newer runtime that supports framework roll-forward
- Docker Desktop or compatible Docker Engine for `docker compose`
- Optional: Ollama running on the host at `http://localhost:11434`

The project targets `net8.0`. Package versions are pinned in the project files
and recorded in `docs/PHASE_02_SPRINT_02_REPORT.md`.

## Environment Setup

Copy `.env.example` to `.env` for local Docker usage and change
`CHROMA_DEV_AUTH_TOKEN` before normal development.

Important defaults:

- `CHROMA_BACKEND_HOST=localhost`
- `CHROMA_BACKEND_PORT=5127`
- `CHROMA_REQUIRE_POSTGRES=true`
- `CHROMA_REQUIRE_OLLAMA=false`
- `CHROMA_ALLOW_LAN_BINDING=false`

LAN binding is rejected unless `CHROMA_ALLOW_LAN_BINDING=true`. Docker Compose
sets LAN binding inside the backend container and binds host ports to
`127.0.0.1`.

## Build And Test

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
```

Automated tests include unit tests, WebSocket contract tests, and PostgreSQL
integration tests using Testcontainers. They do not require live Ollama.

## Database Migration

Install `dotnet-ef` if needed:

```powershell
dotnet tool install --global dotnet-ef --version 8.0.23
```

Create the Sprint 2 migration:

```powershell
dotnet ef migrations add Sprint02ProtocolSupport --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend --output-dir Persistence/Migrations
```

Apply migrations to the configured PostgreSQL database:

```powershell
dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend
```

The Sprint 2 migration creates only protocol-support tables:

- `Workspaces`
- `WorkflowExecutions`
- `WorkflowSessions`
- `ExecutionEvents`
- `EventAcknowledgements`

## Docker Compose

```powershell
docker compose config
docker compose up --build
```

Compose starts:

- `postgres` using `postgres:16-alpine`
- `backend` built from `backend/src/ChromaAgentics.Backend/Dockerfile`
- named volume `chroma-postgres-data`

Ollama is not containerized by default. From the backend container, the default
Ollama URL is `http://host.docker.internal:11434`.

## Health Smoke Tests

With the backend running on port `5127`:

```powershell
curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies
```

Expected behavior:

- `/health/live` returns `200` whenever the process is running.
- `/health/ready` returns `200` when required dependencies are healthy and `503`
  when a required dependency is unavailable.
- `/health/dependencies` returns `200` with PostgreSQL and Ollama status details.
- Optional Ollama failures do not fail readiness unless
  `CHROMA_REQUIRE_OLLAMA=true`.

## WebSocket Smoke Test

Use the header token for normal development. Query-string `devToken` exists only
for local smoke testing.

The smoke flow for protocol `0.2` is:

1. Connect to `ws://localhost:5127/ws/events` with `X-Chroma-Dev-Token`.
2. Receive non-durable `connection.ready`.
3. Send `workflow.start` with `workspaceId`, `workflowId`, `sessionId`, and
   optional `idempotencyKey`.
4. Receive durable `workflow.started` and `workflow.status` with sequences `1`
   and `2`.
5. Send `event.ack` with `lastSeenSequence: 2`.
6. Disconnect and reconnect.
7. Send `session.resume` with the last processed sequence.
8. Confirm only missed durable events are replayed in ascending sequence order.

Concrete PowerShell smoke script:

```powershell
$uri = [Uri]"ws://localhost:5127/ws/events"
$token = $env:CHROMA_DEV_AUTH_TOKEN
if ([string]::IsNullOrWhiteSpace($token)) {
  $token = "change-me-local-dev-token"
}

$workspaceId = [guid]::NewGuid().ToString()
$workflowId = [guid]::NewGuid().ToString()
$sessionId = [guid]::NewGuid().ToString()
$idempotencyKey = "manual-smoke-" + [guid]::NewGuid().ToString()

function New-Client {
  $client = [System.Net.WebSockets.ClientWebSocket]::new()
  $client.Options.SetRequestHeader("X-Chroma-Dev-Token", $token)
  $null = $client.ConnectAsync($uri, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
  return $client
}

function Receive-Envelope($client) {
  $buffer = New-Object byte[] 16384
  $segment = [ArraySegment[byte]]::new($buffer)
  $result = $client.ReceiveAsync($segment, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
  $json = [Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
  return $json | ConvertFrom-Json
}

function Send-Envelope($client, $name, $payload, $idempotencyKeyValue = $null) {
  $message = [ordered]@{
    protocolVersion = "0.2"
    messageId = [guid]::NewGuid().ToString()
    workspaceId = $workspaceId
    workflowId = $workflowId
    sessionId = $sessionId
    sequence = $null
    name = $name
    correlationId = $null
    idempotencyKey = $idempotencyKeyValue
    timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    payload = $payload
  }

  $json = $message | ConvertTo-Json -Depth 20 -Compress
  $bytes = [Text.Encoding]::UTF8.GetBytes($json)
  $segment = [ArraySegment[byte]]::new($bytes)
  $null = $client.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult()
}

$client = New-Client
$ready = Receive-Envelope $client

Send-Envelope $client "workflow.start" ([ordered]@{
  title = "Smoke test workflow"
  mode = "orchestrator"
  source = "manual-smoke-test"
}) $idempotencyKey
$started = Receive-Envelope $client
$status = Receive-Envelope $client

Send-Envelope $client "event.ack" ([ordered]@{ lastSeenSequence = 1 })
$ack = Receive-Envelope $client

Send-Envelope $client "session.resume" ([ordered]@{ lastSeenSequence = 1 })
$replayed = Receive-Envelope $client

Send-Envelope $client "session.resume" ([ordered]@{ lastSeenSequence = 2 })
$current = Receive-Envelope $client

Send-Envelope $client "session.resume" ([ordered]@{ lastSeenSequence = 999 })
$future = Receive-Envelope $client

Send-Envelope $client "workflow.start" ([ordered]@{
  source = "manual-smoke-test"
  mode = "orchestrator"
  title = "Smoke test workflow"
}) $idempotencyKey
$duplicateStarted = Receive-Envelope $client
$duplicateStatus = Receive-Envelope $client

Send-Envelope $client "workflow.start" ([ordered]@{
  title = "Changed smoke workflow"
  mode = "orchestrator"
  source = "manual-smoke-test"
}) $idempotencyKey
$conflict = Receive-Envelope $client
$client.Dispose()

"ready=$($ready.name):$($ready.sequence):$($ready.protocolVersion)"
"started=$($started.name):$($started.sequence) status=$($status.name):$($status.sequence)"
"ack=$($ack.payload.status):$($ack.payload.lastSeenSequence)"
"replayed=$($replayed.name):$($replayed.sequence)"
"current=$($current.payload.status):$($current.sequence)"
"future=$($future.payload.code):$($future.sequence)"
"duplicate=$($duplicateStarted.sequence),$($duplicateStatus.sequence)"
"conflict=$($conflict.payload.code):$($conflict.sequence)"
```

Expected output shape:

```text
ready=connection.ready::0.2
started=workflow.started:1 status=workflow.status:2
ack=ack.updated:1
replayed=workflow.status:2
current=resume.current:
future=future_sequence:
duplicate=1,2
conflict=idempotency_conflict:
```

## VS Code Extension Bridge Smoke Test

Sprint 3 adds an opt-in VS Code extension bridge for protocol `0.2`. The bridge
uses the status bar and an output channel only; it does not add a dashboard or
execute approvals, file edits, terminal commands, MCP actions, or tools.

Extension settings:

```json
{
	"chromaAgentics.backend.enabled": false,
	"chromaAgentics.backend.url": "http://localhost:5127",
	"chromaAgentics.backend.connectionTimeoutMs": 5000,
	"chromaAgentics.backend.reconnect.enabled": true,
	"chromaAgentics.backend.reconnect.maxAttempts": 5,
	"chromaAgentics.backend.reconnect.initialDelayMs": 1000
}
```

Command Palette commands:

- `Chroma Backend: Set/Replace Backend Token`
- `Chroma Backend: Clear Backend Token`
- `Chroma Backend: Test Backend Health`
- `Chroma Backend: Connect Backend Event Stream`
- `Chroma Backend: Start Backend Smoke Workflow`
- `Chroma Backend: Disconnect Backend Event Stream`

Smoke flow:

1. Start the backend on `http://localhost:5127`.
2. Run `Set/Replace Backend Token` and enter the value of
   `CHROMA_DEV_AUTH_TOKEN`.
3. Run `Test Backend Health` and confirm the output channel shows safe health
   metadata.
4. Run `Connect Backend Event Stream` and confirm `connection.ready` is received.
5. Run `Start Backend Smoke Workflow`.
6. Confirm durable `workflow.started` and `workflow.status` are received and ACKed.
7. Run `Disconnect Backend Event Stream`, then connect again.
8. Confirm reconnect sends `session.resume` and duplicate replay display is
   suppressed.

Expected protocol `0.2` output shape:

```text
connection.ready eventName=connection.ready
workflow.start sent eventName=workflow.start
event received eventName=workflow.started sequence=1
event.ack sent sequence=1
event received eventName=workflow.status sequence=2
event.ack sent sequence=2
session.resume sent sequence=2
resume complete eventName=workflow.status
```

If `chromaAgentics.backend.enabled=false`, activation registers commands and
creates the status bar but does not poll health or open a WebSocket. Explicit
Command Palette actions may still run.

## Security Notes

- Backend defaults to localhost binding.
- LAN binding requires `CHROMA_ALLOW_LAN_BINDING=true`.
- Broad CORS is not enabled.
- `X-Chroma-Dev-Token` is required for WebSocket auth.
- Startup and health logging use redacted configuration metadata.
- Protocol logs should include only metadata such as workflow ID, session ID,
  sequence, message name, correlation ID, result, and error code.
- Backend does not execute file edits, terminal commands, MCP tools, tool calls,
  or approval decisions.

## Troubleshooting

- `401` on `/ws/events`: set `CHROMA_DEV_AUTH_TOKEN` and send the same value in
  `X-Chroma-Dev-Token`.
- `503` on `/health/ready`: inspect `/health/dependencies`; required PostgreSQL
  is likely unavailable or not configured.
- Migration connection failure: start PostgreSQL with `docker compose up -d
postgres` or point `CHROMA_DATABASE_CONNECTION_STRING` at a running local
  PostgreSQL instance.
- Replay mismatch: verify the client sends the last processed durable sequence,
  not the last non-durable status. `connection.ready`, ACK statuses,
  `resume.current`, and errors use `sequence: null` and must not advance
  `lastSeenSequence`.
- `future_sequence` on resume: the client is ahead of the database. Re-read the
  latest durable event sequence and retry with a lower `lastSeenSequence`.
- `idempotency_conflict`: the same idempotency key was reused with a different
  canonical payload. Reuse the original payload for retries or send a new
  idempotency key for a new workflow-start attempt.
- Extension bridge missing token: run `Set/Replace Backend Token`; the token is
  stored only in VS Code SecretStorage and is not logged.
- Extension bridge invalid token: replace the SecretStorage token with the
  current `CHROMA_DEV_AUTH_TOKEN` value and reconnect.
- Extension bridge offline or unhealthy: run `Test Backend Health`, verify
  `chromaAgentics.backend.url`, and inspect `/health/dependencies`.
- Docker backend exits on startup: verify `CHROMA_ALLOW_LAN_BINDING=true` is set
  inside the container if binding to `0.0.0.0`.
- Ollama unhealthy: start Ollama on the host or leave
  `CHROMA_REQUIRE_OLLAMA=false`.
