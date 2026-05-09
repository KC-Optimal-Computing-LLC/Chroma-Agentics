# Phase 2 Sprint 1 Report

## Sprint Goal

Create the first real backend foundation for Chroma Agentics: ASP.NET Core
minimal API, typed local configuration, health endpoints, PostgreSQL/Ollama
dependency status, minimal authenticated WebSocket event streaming, tests,
Docker compose, and documentation.

## Scope Delivered

- C#/.NET backend solution under `backend/`
- Health endpoints: `/health/live`, `/health/ready`, `/health/dependencies`
- Typed `CHROMA_*` configuration with localhost default and LAN opt-in validation
- Real PostgreSQL and Ollama health probes behind testable interfaces
- Minimal authenticated `/ws/events` stream emitting one `workflow.status` envelope
- Starter protocol and future approval contract models
- Unit/API tests for health, redaction, WebSocket auth/event behavior, and LAN binding validation
- `.env.example`, `docker-compose.yml`, backend Dockerfile, and backend docs

## Package Versions

- Target framework: `net8.0`
- `Npgsql` `8.0.9`
- `Microsoft.AspNetCore.Mvc.Testing` `8.0.23`
- `Microsoft.NET.Test.Sdk` `17.14.1`
- `xunit` `2.9.3`
- `xunit.runner.visualstudio` `2.8.2`
- `coverlet.collector` `6.0.4`

Both backend projects set `RollForward=Major` so this workstation can run the
`net8.0` app/tests with the installed newer ASP.NET Core runtime. Docker uses
the .NET 8 SDK/runtime images.

## Files Created Or Modified

- Created `backend/ChromaAgentics.Backend.sln`
- Created `backend/src/ChromaAgentics.Backend/`
- Created `backend/tests/ChromaAgentics.Backend.Tests/`
- Created `.env.example`
- Created `docker-compose.yml`
- Created `docs/API_CONTRACT.md`
- Created `docs/GETTING_STARTED_BACKEND.md`
- Created `docs/PHASE_02_SPRINT_01_REPORT.md`
- Modified `.gitignore`
- Modified `README.md`

## Validation Results

Final validation ran on May 9, 2026. Health and WebSocket smoke tests used a
locally started backend with:

- `CHROMA_BACKEND_HOST=localhost`
- `CHROMA_BACKEND_PORT=5127`
- `CHROMA_DATABASE_CONNECTION_STRING=` empty
- `CHROMA_REQUIRE_POSTGRES=true`
- `CHROMA_REQUIRE_OLLAMA=false`
- `CHROMA_OLLAMA_BASE_URL=http://127.0.0.1:1`
- `CHROMA_DEV_AUTH_TOKEN` set to a smoke-test value

| Command                                                            | Exit | Output summary                                                                                                                                                                           |
| ------------------------------------------------------------------ | ---: | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `dotnet restore backend/ChromaAgentics.Backend.sln`                |    0 | `All projects are up-to-date for restore.`                                                                                                                                               |
| `dotnet build backend/ChromaAgentics.Backend.sln`                  |    0 | `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.                                                                                                                                 |
| `dotnet test backend/ChromaAgentics.Backend.sln`                   |    0 | `Passed! - Failed: 0, Passed: 10, Skipped: 0, Total: 10`.                                                                                                                                |
| `docker compose config`                                            |    0 | Rendered `backend` and `postgres` services, loopback host port bindings, PostgreSQL healthcheck, and `chroma-postgres-data` named volume.                                                |
| `curl http://localhost:5127/health/live`                           |    0 | HTTP `200 OK`; body contained `{"status":"healthy","service":"chroma-agentics-backend","timestampUtc":"...Z"}`.                                                                          |
| `curl http://localhost:5127/health/ready`                          |    0 | HTTP `503 Service Unavailable` as expected for missing required PostgreSQL; body included PostgreSQL `not_configured`, `required:true`, and optional Ollama status.                      |
| `curl http://localhost:5127/health/dependencies`                   |    0 | HTTP `200 OK`; body included structured PostgreSQL and Ollama dependency objects with safe error summaries.                                                                              |
| PowerShell `ClientWebSocket` smoke test using `X-Chroma-Dev-Token` |    0 | Received one `workflow.status` envelope with `protocolVersion:"0.1"`, `sequence:1`, generated `messageId`, `workflowId`, `sessionId`, UTC `timestamp`, and `payload.status:"connected"`. |

`docker compose up --build` was not run to avoid leaving local containers or
volumes behind during this pass. Compose syntax and resolved configuration were
validated with `docker compose config`; the backend was smoke-tested directly via
`dotnet run --no-build --no-launch-profile`.

## Endpoint Behavior

- `/health/live` is process-only liveness and does not check dependencies.
- `/health/ready` always returns dependency details and returns `503` when any required dependency is unavailable or not configured.
- `/health/dependencies` returns structured PostgreSQL and Ollama statuses and safe error summaries.
- Secrets are redacted from health output.

## WebSocket Behavior

- `/ws/events` requires `X-Chroma-Dev-Token`.
- Missing or invalid tokens return HTTP `401` before WebSocket upgrade when possible.
- A valid connection receives one `workflow.status` envelope with protocol version, message ID, sequence, name, timestamp, and payload.
- Client-initiated graceful close is supported.
- Query-string `devToken` is smoke-test only and less secure than the header.

## Unit Tests Versus Smoke Tests

Automated unit/API tests:

- Health endpoint response behavior
- Mocked PostgreSQL and Ollama readiness states
- Secret redaction
- WebSocket missing/invalid/valid token behavior
- LAN binding validation

Optional integration/manual smoke tests:

- `docker compose up --build`
- Health `curl` commands against a running backend
- PowerShell `ClientWebSocket` smoke test

## Known Gaps

- No Microsoft Agent Framework workflow execution
- No replay/ACK protocol
- No real approval execution
- No extension bridge UI or reconnect flow
- No Ollama chat adapter or model discovery
- No EF Core schema or migrations
- No pgvector, embeddings, memory, or RAG
- No backend file edit, terminal, or MCP execution

## Risks

- Development token auth is a bootstrap mechanism only and should be replaced or hardened before network exposure.
- Docker compose uses a local development PostgreSQL password by default; users must change it for non-local use.
- The backend has no durable event persistence yet, so WebSocket replay is not available.
- Ollama health checks only verify `/api/tags`; no chat or model capability behavior is implemented.

## Recommended Next Sprint

Implement the extension/backend connection handshake and a contract-tested event
loop skeleton without adding execution privileges to the backend. Keep approval,
file edits, terminal commands, and MCP execution in the Roo-derived extension
path.
