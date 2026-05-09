# Phase 2 Sprint 1 Backend Foundation Plan

## Inspection Summary

- Repo is a Roo-derived TypeScript/pnpm workspace with no existing `backend/`, `db/`, `docs/`, `.env.example`, root `docker-compose.yml`, `.sln`, `.csproj`, or `.cs` files.
- Source docs are `README.md` and `ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md`; both require local-first backend behavior, localhost default binding, PostgreSQL/Ollama health visibility, WebSocket auth, and strict extension/backend approval boundaries.
- Target `net8.0`. Pin new packages: `Npgsql` `8.0.9`, `Microsoft.AspNetCore.Mvc.Testing` `8.0.23`, `Microsoft.NET.Test.Sdk` `17.14.1`, `xunit` `2.9.3`, `xunit.runner.visualstudio` `2.8.2`, `coverlet.collector` `6.0.4`.

## Implementation Changes

- Create `backend/ChromaAgentics.Backend.sln` with ASP.NET Core minimal API and xUnit test projects.
- Add typed config for all required `CHROMA_*` variables. Defaults: host `localhost`, port `5127`, environment `Development`, require Postgres `true`, require Ollama `false`, allow LAN binding `false`, Ollama URL `http://localhost:11434`.
- Enforce startup validation: non-loopback host binding requires `CHROMA_ALLOW_LAN_BINDING=true`.
- Add structured startup logging with redacted config only. Never log tokens, full connection strings, prompts, or secret values.
- Do not enable broad CORS. If local browser testing needs CORS, restrict it to documented localhost origins only.
- Put dependency probes behind interfaces, such as `IPostgresHealthProbe` and `IOllamaHealthProbe`, so tests mock dependency states.
- Update `.gitignore` so `.env.example` is tracked while `.env` and real `.env.*` files stay ignored.
- Add `.env.example`, `docker-compose.yml`, and backend Dockerfile. Compose includes backend, PostgreSQL, named volume, PostgreSQL healthcheck, localhost-bound backend port, and no default Ollama container.

## API And WebSocket Contracts

- `GET /health/live` returns process liveness only:

```json
{
	"status": "healthy",
	"service": "chroma-agentics-backend",
	"timestampUtc": "2026-05-09T20:00:00.0000000Z"
}
```

- `GET /health/ready` returns `200` when required dependencies are healthy, otherwise `503`. It must always include the dependency list so callers can see optional degraded dependencies even when overall readiness is healthy.

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
		}
	]
}
```

- `GET /health/dependencies` returns `200`; top-level `status` is `healthy`, `degraded`, or `unhealthy`. Dependency statuses are `healthy`, `unhealthy`, or `not_configured`.
- `/ws/events` requires `X-Chroma-Dev-Token`; missing or invalid tokens return HTTP `401` before WebSocket upgrade when possible.
- Query-string `devToken` is allowed only for smoke-test documentation, must be documented as less secure than `X-Chroma-Dev-Token`, and must not be used for normal development workflows.
- Valid WebSocket connections emit one `workflow.status` envelope with `protocolVersion`, `messageId`, `sequence`, `name`, `timestamp`, and payload, then support graceful client close.

## Tests And Validation

- Unit tests cover health liveness, readiness success/failure with mocked probes, dependency status shape, secret redaction, WebSocket missing/invalid/valid token behavior, and startup validation rejecting non-loopback hosts unless `CHROMA_ALLOW_LAN_BINDING=true`.
- Optional integration/manual smoke tests: `docker compose up --build`, health `curl` commands, and PowerShell `ClientWebSocket` smoke test using `X-Chroma-Dev-Token`.
- Required commands: `dotnet restore`, `dotnet build`, `dotnet test`, and `docker compose config`.
- Sprint report must include command, exit status, and key output summaries for restore, build, test, compose config, health curls, and WebSocket smoke test.

## Documentation And Deferred Work

- `docs/API_CONTRACT.md` documents exact implemented health and WebSocket shapes, plus security/auth notes.
- Clearly mark replay/ACK, real approval execution, extension bridge, MAF, Ollama chat, pgvector, and RAG as planned, not implemented.
- `docs/GETTING_STARTED_BACKEND.md` covers env setup, Docker, health curls, WebSocket smoke test, and troubleshooting.
- `docs/PHASE_02_SPRINT_01_REPORT.md` records package versions, files changed, validation evidence, known gaps, risks, and next sprint recommendation.
- Do not implement or claim file execution, terminal execution, MCP execution, real approval execution, EF migrations, MAF workflows, Ollama chat, model discovery, pgvector, or RAG.
