# Agent Instruction: Phase 2 Sprint 1 — Backend Foundation + Contract Slice

You are the implementation agent for Chroma Agentics Phase 2 Sprint 1.

## Mission

Create the first real backend foundation for Chroma Agentics.

This sprint must deliver a C#/.NET backend skeleton, health endpoints, typed configuration, local development setup, starter API contract, a WebSocket endpoint supporting authentication, connected status event emission, and graceful closure, automated tests, and documentation.

Build a small working vertical slice. Do not implement placeholders or simulate functionality for systems planned for later phases.

## Source of Truth

Use:
- `README.md`
- `ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md`

The architecture is local-first:
- VS Code Roo fork remains the UI, approval, file-edit, terminal, and MCP execution layer.
- Backend proposes and streams workflow events.
- Backend must not directly execute sensitive file/terminal actions.
- Approved actions execute through inherited Roo/Roo-derived approval tooling.
- Backend defaults to localhost-only binding.
- PostgreSQL is the durable state target.
- Ollama is the default local model provider.
- Cloud providers are opt-in only.

## Sprint Goal

Implement the Phase 2 Sprint 1 foundation:

1. C#/.NET backend solution under `backend/`.
2. ASP.NET Core minimal API.
3. Health endpoints:
   - `GET /health/live`
   - `GET /health/ready`
   - `GET /health/dependencies`
4. Typed environment/config loading.
5. `.env.example`.
6. `docker-compose.yml` for PostgreSQL + backend.
7. Minimal authenticated WebSocket event stream endpoint.
8. Starter protocol envelope types.
9. Starter `docs/API_CONTRACT.md`.
10. Backend setup documentation.
11. Automated tests.
12. Sprint report.

## In Scope

### Backend Skeleton

- .NET 8+ unless repo standards require newer.
- ASP.NET Core minimal API.
- Dependency injection.
- Typed options/config classes.
- Modular folders for config, health, contracts, and streaming.

### Health

Implement:

- `/health/live`: process liveness only.
- `/health/ready`: readiness based on required dependencies.
- `/health/dependencies`: structured status of PostgreSQL and Ollama.

**Availability Rules:**

- PostgreSQL should block readiness only when configured as required. If PostgreSQL is unavailable and required, return a 503 status with a structured error message from `/health/ready`.
- Ollama should appear in dependency status but not block readiness unless required. If Ollama is unavailable, report it in `/health/dependencies` without affecting readiness.
- Liveness must not depend on PostgreSQL or Ollama.

**Security Rules:**

- Do not expose secrets.

### Configuration

Support environment variables:

```text
CHROMA_BACKEND_HOST
CHROMA_BACKEND_PORT
CHROMA_BACKEND_ENVIRONMENT
CHROMA_DATABASE_CONNECTION_STRING
CHROMA_OLLAMA_BASE_URL
CHROMA_REQUIRE_POSTGRES
CHROMA_REQUIRE_OLLAMA
CHROMA_DEV_AUTH_TOKEN
CHROMA_ALLOW_LAN_BINDING
'''

**Network Rules:**

- Default host must be localhost or loopback.
- LAN binding requires explicit opt-in.

**Security Rules:**

- Dev token is required for WebSocket connections.
- Secrets must never be logged or returned from APIs.
Minimal WebSocket Streaming

Implement a minimal endpoint such as:

/ws/events

It must:

require the dev token
accept or generate a session/workflow context
emit at least a connected/status event using the protocol envelope shape
support graceful close
not claim full replay/ACK support yet

Create starter contract models for:

protocol envelope
event names
error event
workflow status event
future approval request/decision placeholders

Do not implement full durable replay, ACK semantics, approval execution, or extension bridge yet.

Local Development

Add:

.env.example
docker-compose.yml

Docker compose must include:

backend service
PostgreSQL service
named PostgreSQL volume
environment variable usage
reasonable health checks where practical

Ollama should remain a host dependency by default. Document optional Ollama behavior separately.

Documentation

Create or update:

docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md
docs/PHASE_02_SPRINT_01_REPORT.md

Docs must clearly state what is implemented vs. planned.

Explicitly Out of Scope

Do not implement:

Microsoft Agent Framework workflows
full replay/ACK protocol
real extension connection UI
file edit execution
terminal command execution
real approval execution
Ollama chat adapter
model discovery
pgvector RAG
EF Core production schema/migrations
LangGraph
n8n
Next.js dashboard
cloud provider adapters

Interfaces/placeholders are allowed only when clearly marked as future-facing and not wired as fake working features.

Required Workflow
1. Inspect First

Before editing:

inspect repo structure
check whether backend/, db/, docs/, .env.example, and docker-compose.yml exist
identify existing .NET files
inspect README and architecture docs
identify conflicts, naming conventions, and risks

Output inspection summary before changes.

2. Plan

Output:

files to create/modify
commands to run
validation strategy
sprint risks
deferred work
3. Implement in Small Slices

Order:

solution/project skeleton
config/options
health endpoints
dependency status checks
protocol contract models
minimal WebSocket streaming endpoint
docker/env files
tests
docs/report
4. Validate

Run:

dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config

If safe:

docker compose up --build

Test health endpoints and WebSocket manually if possible.

5. Report

End with:

summary
files created/modified
commands run
test results
health endpoint behavior
WebSocket behavior
known gaps
risks
next sprint recommendation
Acceptance Criteria

Sprint is complete when:

backend compiles
backend starts locally
/health/live works
/health/ready reflects required dependencies
/health/dependencies reports PostgreSQL and Ollama separately
WebSocket endpoint emits a basic authenticated status/event message
.env.example documents local configuration
docker-compose defines backend + PostgreSQL
health and minimal stream behavior have tests
docs/API_CONTRACT.md documents implemented and planned protocol behavior
getting-started docs exist
report captures validation evidence
no later-phase feature is falsely presented as complete
Guardrails
Do not bypass Roo approval flows.
Do not execute file or terminal actions from backend.
Do not expose LAN binding by default.
Do not log tokens, connection strings, prompts, or secrets.
Do not claim MAF, Ollama chat, RAG, or pgvector works unless actually implemented and tested.
Prefer honest gaps over fake progress.
Behavior

Be precise, skeptical, and implementation-focused. Preserve architectural boundaries. Build the smallest real backend slice that future orchestration, persistence, model provider, replay, ACK, and extension bridge work can safely extend.