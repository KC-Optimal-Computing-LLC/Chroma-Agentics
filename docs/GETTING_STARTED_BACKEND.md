# Getting Started With The Backend

This guide starts the Phase 2 Sprint 1 backend foundation. The backend is a
local-first ASP.NET Core minimal API under `backend/`.

## Prerequisites

- .NET SDK capable of building `net8.0`
- ASP.NET Core runtime 8.0, or a newer runtime that supports framework roll-forward
- Docker Desktop or compatible Docker Engine for `docker compose`
- Optional: Ollama running on the host at `http://localhost:11434`

The project targets `net8.0`. Package versions are pinned in the project files
and recorded in `docs/PHASE_02_SPRINT_01_REPORT.md`.

## Environment Setup

Copy `.env.example` to `.env` for local Docker usage and change
`CHROMA_DEV_AUTH_TOKEN` before normal development.

Important defaults:

- `CHROMA_BACKEND_HOST=localhost`
- `CHROMA_BACKEND_PORT=5127`
- `CHROMA_REQUIRE_POSTGRES=true`
- `CHROMA_REQUIRE_OLLAMA=false`
- `CHROMA_ALLOW_LAN_BINDING=false`

LAN binding is rejected unless `CHROMA_ALLOW_LAN_BINDING=true`. Docker compose
sets LAN binding only inside the backend container and binds the host port to
`127.0.0.1`.

## Build And Test

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
```

The automated tests are unit/API tests using mocked PostgreSQL and Ollama probes.
They do not require live external services.

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
- `/health/ready` returns `200` when required dependencies are healthy and `503` when a required dependency is unavailable.
- `/health/dependencies` returns `200` with PostgreSQL and Ollama status details.
- Optional Ollama failures appear as degraded dependency status and do not fail readiness unless `CHROMA_REQUIRE_OLLAMA=true`.

## WebSocket Smoke Test

Use the header token for normal development:

```powershell
$token = "change-me-local-dev-token"
$socket = [System.Net.WebSockets.ClientWebSocket]::new()
$socket.Options.SetRequestHeader("X-Chroma-Dev-Token", $token)
$uri = [Uri]"ws://localhost:5127/ws/events"
$ct = [Threading.CancellationToken]::None
$socket.ConnectAsync($uri, $ct).GetAwaiter().GetResult()
$buffer = [byte[]]::new(4096)
$result = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), $ct).GetAwaiter().GetResult()
[Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
$socket.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $ct).GetAwaiter().GetResult()
```

The `devToken` query parameter exists only for smoke-test convenience. It is less
secure than `X-Chroma-Dev-Token` because URLs can be stored in logs, shell
history, browser history, and proxy logs.

## Troubleshooting

- `401` on `/ws/events`: set `CHROMA_DEV_AUTH_TOKEN` and send the same value in `X-Chroma-Dev-Token`.
- `503` on `/health/ready`: inspect `/health/dependencies`; required PostgreSQL is likely unavailable or not configured.
- Docker backend exits on startup: verify `CHROMA_ALLOW_LAN_BINDING=true` is set inside the container if binding to `0.0.0.0`.
- Ollama unhealthy: start Ollama on the host or leave `CHROMA_REQUIRE_OLLAMA=false`.
- Runtime missing locally: install ASP.NET Core Runtime 8.0 or use a newer compatible runtime with roll-forward.
