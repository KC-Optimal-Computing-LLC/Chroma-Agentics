Below is a **Verification & Validation prompt** for **Phase 2 Sprint 1 Backend Foundation**. It is designed for a separate review/QA agent after the coding agent finishes implementation. Its job is to inspect, test, and either approve or reject the sprint against the plan. Ruthless little creature, as all QA prompts should be. The scope is based on the updated Sprint 1 plan: .NET backend skeleton, health endpoints, typed config, Docker setup, minimal authenticated WebSocket stream, tests, docs, and strict deferred-work boundaries.

Save as:

```text
prompts/phase-02-sprint-01-verification-validation.md
```

````markdown
# Verification & Validation Prompt: Phase 2 Sprint 1 Backend Foundation

You are the Verification & Validation Agent for Chroma Agentics Phase 2 Sprint 1.

Your job is to independently verify whether the implementation satisfies the approved Sprint 1 Backend Foundation Plan.

Approach the review with a neutral stance, requiring evidence for both success and failure. Inspect the repository, run validation commands, review files, check API behavior, verify tests, and produce a pass/fail report with evidence.

## Source of Truth

Use the approved Sprint 1 plan and repository contents as the source of truth.

Sprint 1 requires:

- C#/.NET backend skeleton under `backend/`
- ASP.NET Core minimal API targeting `net8.0`
- health endpoints
- typed `CHROMA_*` configuration
- safe localhost-first binding
- minimal authenticated `/ws/events` WebSocket stream
- protocol contract models
- `.env.example`
- `docker-compose.yml`
- backend Dockerfile
- automated tests
- docs
- sprint report
- no fake later-phase integrations

## Primary Objective

Determine whether Sprint 1 is complete, partially complete, or failed.

You must verify both:

1. **Validation**: Did the team build the correct thing according to the plan?
2. **Verification**: Did they build it correctly, with passing tests and observable behavior?

Do not fix implementation unless explicitly instructed. This is a review and evidence-gathering task.

**Validation Checklist (complete in order):**

1. Inspect repository structure and file presence
2. Confirm scope discipline (no out-of-scope features)
3. Verify package and framework requirements
4. Run required build/test commands
5. Validate API health endpoints
6. Validate dependency availability behavior
7. Validate secret redaction
8. Validate WebSocket endpoint
9. Validate typed configuration
10. Validate Docker setup
11. Validate test suite
12. Validate documentation
13. Produce final report

---

# Required Review Workflow

## 1. Inspect Repository State

Verify whether these exist:

```text
backend/
  ChromaAgentics.Backend.sln
  src/ChromaAgentics.Backend/
  tests/ChromaAgentics.Backend.Tests/

docs/
  API_CONTRACT.md
  GETTING_STARTED_BACKEND.md
  PHASE_02_SPRINT_01_REPORT.md

.env.example
docker-compose.yml
```
````

Also inspect:

```text
.gitignore
README.md
backend/src/ChromaAgentics.Backend/Dockerfile
```

Report missing, extra, or suspicious files.

## 2. Confirm Scope Discipline

Fail or flag the implementation if it claims or implements unsupported later-phase features.

Sprint 1 must **not** implement or falsely claim completion of:

- Microsoft Agent Framework workflows
- full replay/ACK WebSocket protocol
- real extension bridge UI
- real approval execution
- file edit execution from backend
- terminal execution from backend
- Ollama chat adapter
- model discovery
- pgvector RAG
- EF Core production migrations
- LangGraph
- n8n
- Next.js dashboard
- cloud provider adapters
- full Phase 2 completion

Contract-only placeholders are allowed only if clearly documented as future-facing.

## 3. Verify Package and Framework Requirements

Check:

- backend targets `net8.0`
- solution builds through `backend/ChromaAgentics.Backend.sln`
- package versions are pinned, not floating
- expected package versions are documented in sprint report

Expected package pins:

```text
Backend:
- Npgsql 8.0.9

Tests:
- Microsoft.AspNetCore.Mvc.Testing 8.0.23
- Microsoft.NET.Test.Sdk 17.14.1
- xunit 2.9.3
- xunit.runner.visualstudio 2.8.2
- coverlet.collector 6.0.4
```

Flag version drift unless the implementation documents a valid reason.

---

# Required Command Validation

Run these commands from the repository root:

```bash
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

Record for each:

- command
- pass/fail
- exit code if visible
- important output lines
- whether failure is environmental or implementation-related

If safe, run:

```bash
docker compose up --build
```

Then test health endpoints manually.

Do not claim validation passed unless commands actually pass.

---

# API Validation

## 1. Health Endpoint Contract

Verify:

```http
GET /health/live
GET /health/ready
GET /health/dependencies
```

### `/health/live`

Must:

- return HTTP `200`
- not call PostgreSQL or Ollama dependency probes
- return:

```json
{
	"status": "healthy",
	"service": "chroma-agentics-backend",
	"timestampUtc": "ISO-8601"
}
```

### `/health/ready`

Must:

- return HTTP `200` when all required dependencies are healthy
- return HTTP `503` when required PostgreSQL is unhealthy
- keep liveness independent from readiness
- always include dependency details

Expected shape:

```json
{
	"status": "healthy | degraded | unhealthy",
	"service": "chroma-agentics-backend",
	"timestampUtc": "ISO-8601",
	"dependencies": [
		{
			"name": "postgresql",
			"status": "healthy | unhealthy | not_configured",
			"required": true,
			"checkedAtUtc": "ISO-8601",
			"error": null
		}
	]
}
```

### `/health/dependencies`

Must:

- return HTTP `200`
- report PostgreSQL and Ollama separately
- use only these dependency status values:

    - `healthy`
    - `unhealthy`
    - `not_configured`

- use safe error summaries only
- never expose secrets

## 2. Dependency Behavior

Verify:

- PostgreSQL blocks readiness only when `CHROMA_REQUIRE_POSTGRES=true`
- Ollama does not block readiness when `CHROMA_REQUIRE_OLLAMA=false`
- Ollama blocks readiness when `CHROMA_REQUIRE_OLLAMA=true`
- missing optional Ollama does not crash backend
- missing required PostgreSQL fails readiness but not liveness

## 3. Secret Redaction

Search API responses, logs, docs, and test output for accidental exposure of:

- `CHROMA_DEV_AUTH_TOKEN`
- full database connection string
- database password
- raw secrets
- prompts
- provider keys

Fail security validation if secrets appear in health responses or normal logs.

---

# WebSocket Validation

Verify endpoint:

```text
/ws/events
```

## Required Auth Behavior

- missing token returns HTTP `401` before WebSocket upgrade when possible
- invalid token returns HTTP `401` before WebSocket upgrade when possible
- valid token connects successfully
- header auth via `X-Chroma-Dev-Token` is supported
- query `devToken` is documented as smoke-test only and less secure

## Required Event Behavior

On valid connection, server must emit one structured envelope:

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
	"timestamp": "ISO-8601",
	"payload": {
		"status": "connected",
		"detail": "Event stream connected."
	}
}
```

Verify:

- required fields exist
- `messageId`, `workflowId`, and `sessionId` are valid UUIDs
- `sequence` starts at `1`
- `name` is `workflow.status`
- `payload.status` is `connected`
- graceful client close is supported

Flag any claim that replay, ACK, extension bridge, approval execution, MAF workflows, Ollama chat, pgvector, or RAG are implemented.

---

# Configuration Validation

## Variable Presence

Verify typed config exists for each variable:

| Variable                            | Expected Default                  |
| ----------------------------------- | --------------------------------- |
| `CHROMA_BACKEND_HOST`               | `localhost`                       |
| `CHROMA_BACKEND_PORT`               | `5127`                            |
| `CHROMA_BACKEND_ENVIRONMENT`        | `Development`                     |
| `CHROMA_DATABASE_CONNECTION_STRING` | _(none — must be set explicitly)_ |
| `CHROMA_OLLAMA_BASE_URL`            | `http://localhost:11434`          |
| `CHROMA_REQUIRE_POSTGRES`           | `true`                            |
| `CHROMA_REQUIRE_OLLAMA`             | `false`                           |
| `CHROMA_DEV_AUTH_TOKEN`             | _(none — must be set explicitly)_ |
| `CHROMA_ALLOW_LAN_BINDING`          | `false`                           |

## Network Binding Rules

Verify:

- non-loopback host binding fails unless `CHROMA_ALLOW_LAN_BINDING=true`

## Environment File Rules

Verify:

- `.env.example` is tracked in the repository
- `.env`, `.env.local`, and secret-bearing `.env.*` files are listed in `.gitignore`

## Secret Logging Rules

Verify:

- dev token (`CHROMA_DEV_AUTH_TOKEN`) is never written to logs
- full DB connection string (`CHROMA_DATABASE_CONNECTION_STRING`) is never written to logs

---

# Docker Validation

Inspect `docker-compose.yml`.

Verify it includes:

- backend service
- PostgreSQL service
- named PostgreSQL volume
- PostgreSQL healthcheck
- localhost-bound backend port
- environment variable usage
- backend dependency on PostgreSQL health where practical
- no default Ollama container

Verify backend Dockerfile exists and is usable by compose.

Run:

```bash
docker compose config
```

If safe:

```bash
docker compose up --build
```

Record result.

---

# Test Suite Validation

Review and run tests.

Required tests:

## Health Tests

- `/health/live` returns `200`
- `/health/live` does not call dependency probes
- `/health/ready` returns `200` with mocked healthy required dependencies
- `/health/ready` returns `503` when required PostgreSQL is unhealthy
- `/health/dependencies` returns separate PostgreSQL/Ollama statuses

## Security Tests

- responses do not expose dev token
- responses do not expose full connection string
- responses do not expose DB password

## WebSocket Tests

- missing token rejected
- invalid token rejected
- valid token receives one structured event envelope

## Config Tests

- non-loopback binding is rejected unless LAN mode is enabled

Flag missing tests.

Tests should mock dependency probes instead of requiring live PostgreSQL/Ollama for unit tests. Live dependency checks belong in optional integration/manual smoke tests.

---

# Documentation Validation

Verify `docs/API_CONTRACT.md` includes:

- implemented health endpoints
- exact response shapes
- implemented minimal WebSocket stream
- protocol envelope shape
- auth behavior
- extension/backend approval boundary
- planned-only sections for:

    - replay/ACK
    - real approval execution
    - extension bridge
    - Microsoft Agent Framework
    - Ollama chat
    - pgvector
    - RAG

Verify `docs/GETTING_STARTED_BACKEND.md` includes:

- prerequisites
- .NET/runtime expectations
- env setup
- restore/build/test commands
- Docker compose setup
- health curl examples
- WebSocket smoke-test instructions
- troubleshooting

Verify `docs/PHASE_02_SPRINT_01_REPORT.md` includes:

- sprint goal
- scope
- package versions
- files changed
- commands run
- validation summaries
- endpoint behavior
- WebSocket behavior
- known gaps
- risks
- recommended next sprint

Verify README was only minimally updated and does not overstate backend readiness.

---

# Final Report Format

Output your review in this format:

```markdown
# Phase 2 Sprint 1 Verification & Validation Report

## Verdict

PASS / PASS WITH ISSUES / FAIL

## Executive Summary

Brief result summary.

## Validation Matrix

| Area             |    Result | Evidence | Issues |
| ---------------- | --------: | -------- | ------ |
| Repo structure   | PASS/FAIL | ...      | ...    |
| Build            | PASS/FAIL | ...      | ...    |
| Tests            | PASS/FAIL | ...      | ...    |
| Health endpoints | PASS/FAIL | ...      | ...    |
| WebSocket stream | PASS/FAIL | ...      | ...    |
| Config/security  | PASS/FAIL | ...      | ...    |
| Docker           | PASS/FAIL | ...      | ...    |
| Docs             | PASS/FAIL | ...      | ...    |
| Scope discipline | PASS/FAIL | ...      | ...    |

## Commands Run

List exact commands, result, and key output.

## API Results

Include health endpoint and WebSocket observations.

## Security Findings

Include any secret/log/CORS/binding issues.

## Missing or Failed Requirements

List each issue with severity:

- Critical
- High
- Medium
- Low

## Required Fixes Before Approval

Concrete changes needed before Sprint 1 can be accepted.

## Deferred Work Confirmed

List planned-only features correctly deferred.

## Final Recommendation

Approve, approve with fixes, or reject.
```

## Scoring Rules

Use this scoring:

- **PASS**: all required implementation, tests, docs, and validation pass.
- **PASS WITH ISSUES**: core functionality works, but minor docs/tests/config gaps remain.
- **FAIL**: build/test failure, missing health/WebSocket implementation, unsafe secret exposure, broken scope boundary, fake feature claims, or missing required docs.

Critical failures include:

- backend does not build
- test suite does not run
- health endpoints missing
- WebSocket auth missing
- backend executes file/terminal actions
- secrets exposed in health/logs
- LAN exposed by default
- fake claims of MAF/Ollama/RAG/replay/approval execution

````

## Minimal handoff prompt

```text
Use `prompts/phase-02-sprint-01-verification-validation.md` to independently verify the completed Sprint 1 implementation. Do not modify code. Inspect the repo, run the required commands, test health and WebSocket behavior, verify docs/security/scope boundaries, and output the V&V report exactly in the required format.
````
