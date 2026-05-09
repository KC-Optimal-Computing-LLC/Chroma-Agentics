# Code Generation Prompt: Phase 2 Sprint 1 — Backend Foundation + Contract Slice

You are working in the Chroma Agentics repository.

Implement Phase 2 Sprint 1: the first real C#/.NET backend foundation with health endpoints, local configuration, docker-compose, starter API contract models, minimal authenticated WebSocket event streaming, tests, and docs.

## Project Context

Chroma Agentics is an independent Roo Code fork evolving into a local-first agentic development platform.

Architecture rules:

- VS Code extension remains responsible for UI, user approval, file edits, terminal execution, and MCP/tool execution.
- Backend handles future orchestration, persistence, model access, event streaming, and durable workflow state.
- Backend must only propose sensitive actions. It must not execute file/terminal actions directly.
- PostgreSQL + pgvector are planned for durable state, checkpoints, memory, and retrieval.
- Ollama is the default local model provider.
- Cloud providers are opt-in.
- Backend must default to localhost-only binding; LAN binding is opt-in and only permitted when CHROMA_ALLOW_LAN_BINDING=true.

## Sprint Objective

Create a working backend foundation and minimal contract slice.

This sprint should prove:

- backend can start locally
- health endpoints work
- local config works
- PostgreSQL can be represented as a dependency
- Ollama can be represented as an optional dependency
- a minimal authenticated WebSocket stream can emit structured protocol events
- tests can validate the foundation

Do not implement full orchestration, RAG, Ollama chat, or real extension action execution. Creating placeholder contract types (such as approval request/decision types) for future phases is permitted; do not implement the underlying execution logic.

Suggested implementation order: (1) project scaffolding and configuration, (2) health endpoints, (3) minimal WebSocket stream, (4) tests, (5) Docker and documentation.

## Required First Step

Inspect the repo before editing.

Report:

1. existing folder structure
2. whether `backend/`, `db/`, `docs/`, `.env.example`, or `docker-compose.yml` exist
3. existing .NET projects, if any
4. existing docs that should be updated
5. naming conventions
6. conflicts/risks before adding files

Then implement.

## Required File Targets

Create or update, adapting names only if existing files or naming conventions in the repository explicitly conflict with the provided names:

```text
backend/
  ChromaAgentics.Backend.sln
  src/ChromaAgentics.Backend/
    ChromaAgentics.Backend.csproj
    Program.cs
    Configuration/
      BackendOptions.cs
      DependencyOptions.cs
    Health/
      DependencyHealthService.cs
      HealthResponse.cs
    Contracts/
      ProtocolEnvelope.cs
      ProtocolEventNames.cs
      WorkflowStatusPayload.cs
      ErrorPayload.cs
      ApprovalContracts.cs
    Streaming/
      EventStreamEndpoint.cs
      DevTokenAuth.cs
  tests/ChromaAgentics.Backend.Tests/
    ChromaAgentics.Backend.Tests.csproj
    HealthEndpointTests.cs
    EventStreamTests.cs
    SecretRedactionTests.cs

docs/
  API_CONTRACT.md
  GETTING_STARTED_BACKEND.md
  PHASE_02_SPRINT_01_REPORT.md

.env.example
docker-compose.yml
Backend Requirements

Use .NET 8+ and ASP.NET Core minimal API.

Use:

dependency injection
typed options
structured responses
safe logging
clear separation between API, health, config, contracts, and streaming

Do not use secrets in logs or responses.

Health Endpoint Requirements
GET /health/live

Purpose:

process liveness only
must not check PostgreSQL or Ollama

Response shape:

{
  "status": "healthy",
  "service": "chroma-agentics-backend",
  "timestampUtc": "ISO-8601"
}
GET /health/ready

Purpose:

readiness based on required dependencies

Rules:

if PostgreSQL is required and unavailable, readiness is unhealthy
if Ollama is optional and unavailable, readiness remains healthy with degraded/optional dependency status
if Ollama is required and unavailable, readiness is unhealthy
liveness remains healthy even if dependencies are unavailable
GET /health/dependencies

Purpose:

detailed dependency status

Include:

dependency name
status
required boolean
checkedAtUtc
safe error summary if unavailable
never include connection strings, tokens, prompts, or raw secret values
Configuration Requirements

Read these environment variables:

CHROMA_BACKEND_HOST
CHROMA_BACKEND_PORT
CHROMA_BACKEND_ENVIRONMENT
CHROMA_DATABASE_CONNECTION_STRING
CHROMA_OLLAMA_BASE_URL
CHROMA_REQUIRE_POSTGRES
CHROMA_REQUIRE_OLLAMA
CHROMA_DEV_AUTH_TOKEN
CHROMA_ALLOW_LAN_BINDING

Defaults:

host: localhost or loopback
port: 5127 unless repo convention says otherwise
require Postgres: true for compose/dev backend
require Ollama: false
allow LAN binding: false

Rules:

LAN binding requires CHROMA_ALLOW_LAN_BINDING=true
dev token is required for WebSocket connections
missing optional Ollama must not crash backend
missing required Postgres should fail readiness, not liveness
if CHROMA_DATABASE_CONNECTION_STRING is missing or invalid, log a safe error (without exposing the value) and fail readiness checks
never log CHROMA_DEV_AUTH_TOKEN or the full DB connection string
Minimal WebSocket/Event Stream Requirements

Create endpoint:

/ws/events

Authentication:

require dev token via header or query string
prefer header, for example X-Chroma-Dev-Token
reject missing/invalid token

Behavior:

on connection, emit one structured event envelope such as workflow.status or connection.ready
include protocolVersion, messageId, sequence, name, timestamp, and payload
support graceful close
return safe error envelope for invalid input where practical

Do not implement full replay/ACK yet.

Create contract types for the future protocol:

ProtocolEnvelope<TPayload>
event names/constants
workflow status payload
error payload
approval request/decision placeholder types

Document clearly that:

basic WebSocket streaming exists
replay/ACK is planned
approval execution is not implemented
extension bridge is not complete
Docker Requirements

Create docker-compose.yml with:

PostgreSQL service
backend service
named PostgreSQL volume
environment variables
backend dependency on PostgreSQL health where practical
safe local ports

Do not containerize Ollama by default.

Document that Ollama is expected on the host at CHROMA_OLLAMA_BASE_URL, usually http://localhost:11434, unless later explicitly configured otherwise.

Documentation Requirements
docs/API_CONTRACT.md

Must include:

purpose
implemented endpoints
health endpoint response shapes
implemented minimal WebSocket stream
protocol envelope shape
planned replay/ACK semantics
planned approval flow
security/auth notes
extension/backend approval boundary

State clearly what is implemented now vs. planned.

docs/GETTING_STARTED_BACKEND.md

Must include:

prerequisites
environment setup
dotnet restore/build/test
docker compose setup
health endpoint curl examples
WebSocket smoke-test instructions
troubleshooting
docs/PHASE_02_SPRINT_01_REPORT.md

Must include:

sprint goal
scope
files created/modified
commands run
validation results
endpoint behavior
WebSocket behavior
known gaps
risks
recommended next sprint
Testing Requirements

Add automated tests for:

Health:

/health/live returns success
/health/live does not depend on external services
/health/ready succeeds when required dependencies are available or mocked healthy
/health/ready fails when required PostgreSQL is unavailable
/health/dependencies returns structured statuses

Security/redaction:

health responses do not expose dev token
health responses do not expose full database connection string

WebSocket:

missing token is rejected
invalid token is rejected
valid token receives at least one structured event envelope
event includes protocolVersion, messageId, sequence, name, timestamp, payload

Use xUnit or the repo’s existing .NET test convention.

Validation Commands

Run:

dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config

If safe:

docker compose up --build

Then manually test, using the configured port:

curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies

Also perform a WebSocket smoke test with the configured dev token. Document the exact command/tool used.

Hard Constraints

Do not:

implement fake Microsoft Agent Framework workflows
implement fake Ollama chat
implement fake pgvector/RAG
create production EF migrations unless explicitly required by this sprint
execute file edits or terminal commands from the backend
bypass Roo approval flows
expose backend on LAN by default
log secrets
claim full extension bridge works unless implemented and tested
claim replay/ACK works unless implemented and tested
mark all of Phase 2 complete
Final Response Format

When done, output:

Summary
Files created/modified
Commands run
Test results
Health endpoint behavior
WebSocket/event stream behavior
Config/docker behavior
Docs updated
Known gaps
Risks
Next sprint recommendation

Be honest. A small tested vertical slice beats a grand unverified pile of architectural confetti.
```
