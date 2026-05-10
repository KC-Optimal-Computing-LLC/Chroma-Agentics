# Verification & Validation Prompt: Phase 2 Sprint 3 - Extension Bridge + End-to-End Event Streaming

You are the Verification & Validation Agent for Chroma Agentics Phase 2 Sprint 3.

Your job is to independently verify whether the completed Sprint 3 implementation satisfies the approved Sprint 3 plan.

Do not assume success. Inspect files, run commands, test extension behavior, verify backend integration, review docs, check security boundaries, and produce a pass/fail report with evidence.

Do not modify implementation code or repository artifacts unless explicitly instructed. This is review, not repair.

## Source of Truth

Use these as source of truth:

```text
README.md
ChromaAgentics_TARGET_ARCHITECTURE_v1.6.md
docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md
docs/PHASE_02_SPRINT_02_REPORT.md
docs/PHASE_02_SPRINT_03_REPORT.md
docs/schemas/protocol/v0.2/*.schema.json
agents/phase-02-sprint-03-extension-bridge.agent.md
prompts/phase-02-sprint-03-implementation.prompt.md
existing repository implementation
```

For Sprint 3 prompt artifacts in this repo, `.prompt.md` is the canonical
naming convention.

Sprint 3 target:

```text
Phase 2 Sprint 3 - Extension Bridge + End-to-End Event Streaming
```

Protocol version:

```text
0.2
```

The extension bridge must remain narrow:

- health check
- WebSocket connection
- workflow.start
- event.ack
- session.resume
- status/output feedback
- tests/docs

The extension bridge must not implement:

- file edits
- terminal execution
- MCP/tool execution
- approval execution
- Ollama chat
- model streaming
- model discovery
- MAF workflows
- RAG
- pgvector
- dashboard/webview redesign
- Phase 2 completion claim during implementation

## 1. V&V Objective

Determine whether Sprint 3 is:

- PASS
- PASS WITH ISSUES
- FAIL

Verify both:

- Validation: Did the team build the correct thing?
- Verification: Did they build it correctly?

Use this review sequence to reduce ambiguity:

1. Confirm the Sprint 2 backend gate still passes.
2. Verify canonical files and scope discipline.
3. Verify activation, settings, commands, SecretStorage, dependencies, and bridge module boundaries.
4. Verify protocol generation, schema validation, session/replay handling, and runtime behavior.
5. Verify tests, smoke flow, docs, README guard, and Phase 2 exit evidence.

## 2. Sprint 2 Gate Verification

Before evaluating Sprint 3, confirm Sprint 2 still works.

Run:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

If safe, run:

```powershell
docker compose up -d --build backend
curl http://localhost:5127/health/live
curl http://localhost:5127/health/ready
curl http://localhost:5127/health/dependencies
```

Verify:

- Sprint 2 report or acceptance note exists
- backend starts locally
- `/ws/events` requires `X-Chroma-Dev-Token`
- `protocolVersion` is `0.2`
- `workflow.start` works
- `event.ack` works
- `session.resume` works
- durable vs non-durable behavior is documented

Fail if Sprint 3 regresses Sprint 2 backend behavior.

## 3. Canonical File Verification

Verify required Sprint 3 files exist:

```text
agents/phase-02-sprint-03-extension-bridge.agent.md
prompts/phase-02-sprint-03-implementation.prompt.md
prompts/phase-02-sprint-03-verification-validation.prompt.md
docs/EXTENSION_BACKEND_BRIDGE.md
docs/PHASE_02_SPRINT_03_REPORT.md
```

Verify updated docs exist:

```text
docs/API_CONTRACT.md
docs/GETTING_STARTED_BACKEND.md
```

Verify protocol schemas still exist:

```text
docs/schemas/protocol/v0.2/envelope.schema.json
docs/schemas/protocol/v0.2/workflow-start.schema.json
docs/schemas/protocol/v0.2/session-resume.schema.json
docs/schemas/protocol/v0.2/event-ack.schema.json
docs/schemas/protocol/v0.2/error-envelope.schema.json
```

Flag missing files.

## 4. Scope Discipline Verification

Fail if Sprint 3 implements or claims any prohibited system:

- Orchestrator workflow logic
- Microsoft Agent Framework workflows
- Ollama chat
- model streaming
- model discovery
- approval execution
- file edits
- terminal commands
- MCP/tool execution
- RAG
- pgvector
- dashboard/webview redesign
- Next.js
- LangGraph
- n8n
- cloud provider adapters
- production auth
- multi-user authorization
- Phase 2 completion claim during implementation

Verify bridge code does not call:

- `workspace.fs`
- `applyEdit`
- `createTerminal`
- `sendText`
- MCP manager/import paths
- task execution imports
- tool execution imports
- approval execution paths
- webview/dashboard code

A static boundary test or grep-style validation must exist for `src/services/backend-bridge/`.

## 5. Activation and Registration Verification

Verify bridge registration uses the existing extension activation/command registration convention.

Required:

- no second activation system
- commands registered through current extension activation path
- status bar creation allowed on activation
- bridge services lazy where practical
- no network calls when disabled
- no health polling when disabled
- no WebSocket connection when disabled
- explicit user commands may run when automatic bridge behavior is disabled

Inspect activation files and command registration files.

Fail if the backend bridge auto-connects or polls while `chromaAgentics.backend.enabled=false`.

## 6. Extension Settings Verification

Verify settings exist under:

```text
chromaAgentics.backend.*
```

Required settings:

- `chromaAgentics.backend.enabled`
- `chromaAgentics.backend.url`
- `chromaAgentics.backend.connectionTimeoutMs`
- `chromaAgentics.backend.reconnect.enabled`
- `chromaAgentics.backend.reconnect.maxAttempts`
- `chromaAgentics.backend.reconnect.initialDelayMs`

Expected defaults:

- `enabled=false`
- `url=http://localhost:5127`
- `connectionTimeoutMs=5000`
- `reconnect.enabled=true`
- `reconnect.maxAttempts=5`
- `reconnect.initialDelayMs=1000`

Verify matching English `package.nls.json` strings exist.

Verify README or report documents that `chromaAgentics.backend.*` is the Sprint 3 canonical namespace even though the extension package may still be `roo-cline`.

## 7. Command Verification

Verify backend bridge commands exist:

- `chromaAgentics.backend.setToken`
- `chromaAgentics.backend.clearToken`
- `chromaAgentics.backend.health`
- `chromaAgentics.backend.connect`
- `chromaAgentics.backend.startSmokeWorkflow`
- `chromaAgentics.backend.disconnect`

Verify behavior:

- `setToken` stores/replaces token
- `clearToken` removes token
- `health` checks backend health
- `connect` opens `/ws/events` using SecretStorage token
- `startSmokeWorkflow` sends `workflow.start`
- `disconnect` closes bridge connection

Verify `CommandId` union behavior:

- existing Roo command IDs unchanged
- `CommandId` union unchanged, or changed only if required for repo type safety
- if changed, reason documented in `docs/PHASE_02_SPRINT_03_REPORT.md`

Fail if existing Roo commands are renamed or broken.

## 8. SecretStorage Verification

Verify token is stored only in:

```text
ExtensionContext.secrets
```

Expected key:

```text
chromaAgentics.backend.devToken
```

Verify:

- token never stored in package config
- token never stored in settings JSON
- token never logged
- token never shown in output channel
- missing token produces safe status/error
- set token works
- replace token works
- clear token works

Tests must use a SecretStorage mock or equivalent.

## 9. Dependency Verification

Verify dependency decisions:

- health checks use existing fetch/undici behavior
- runtime dependency `ws` pinned exactly to `8.20.0`
- dev dependency `@types/ws` pinned exactly to `8.18.1`
- dev dependency `ajv` pinned exactly to `8.18.0`

If a dependency version differs, verify the reason is documented.

Verify no heavy framework dependency was added for the bridge.

## 10. Bridge Module Verification

Verify isolated bridge code exists under:

```text
src/services/backend-bridge/
```

Expected components:

- config reader
- SecretStorage wrapper
- protocol 0.2 envelope/types and generators
- safe output logger
- status bar controller
- bridge client
- health client or health behavior
- session state handling
- error/failure handling

Verify bridge client is testable with injected:

- fetch
- WebSocket factory
- SecretStorage wrapper or token provider
- output/status sinks

Fail if bridge code is tangled into unrelated Roo command, webview, terminal, file, or MCP logic.

## 11. Protocol 0.2 Verification

Verify extension-created envelopes use:

```text
protocolVersion = 0.2
```

Verify `workflow.start` includes client-generated:

- `workspaceId`
- `workflowId`
- `sessionId`
- `messageId`
- `correlationId`
- `idempotencyKey`
- `timestamp`

Verify extension uses:

- `crypto.randomUUID()`

or a safe existing UUID utility.

Verify WebSocket URL conversion:

- `http -> ws`
- `https -> wss`
- append `/ws/events`
- no query-string token

Verify `X-Chroma-Dev-Token` is sent from SecretStorage only.

## 12. JSON Schema Validation Verification

Verify Ajv tests validate extension-created envelopes against:

```text
docs/schemas/protocol/v0.2/*.schema.json
```

Required schema validation tests:

- `workflow.start` envelope validates
- `event.ack` envelope validates
- `session.resume` envelope validates

Required parsing tests:

- `connection.ready` parsing
- `workflow.started` parsing
- `workflow.status` parsing
- `error` envelope parsing

If schemas are incomplete or incompatible, verify the gap is documented and the affected test is failed or explicitly pending with justification.

Fail if schema drift is silently ignored.

## 13. Session State and Replay Verification

Sprint 3 session state must be memory-only unless persistence is already available, trivial, and tested.

Required in-memory state:

- `workspaceId`
- `workflowId`
- `sessionId`
- `lastSeenSequence`
- `processedMessageIds`
- `connectionState`
- `lastErrorCode`

Verify duplicate replay suppression:

- processed event key = `workflowId + sequence + messageId`
- do not display replayed duplicate events twice
- ACK only after event is processed or intentionally ignored as duplicate

Verify ACK behavior:

- ACK durable events only
- ACK after processing
- ACK uses highest processed sequence
- ACK does not mean approval
- ACK does not trigger file/terminal/tool execution

## 14. Runtime Behavior Verification

Verify:

- on activation, commands/status bar register
- if `enabled=false`, no automatic network activity
- if `enabled=true` and token exists, auto-connect is allowed
- if token missing, safe auth/missing-token status appears
- Test Backend Health may run while disabled
- Connect and Start Smoke Workflow require `enabled=true` and stored token

Verify failure modes are handled safely:

| Condition            | Expected result                                     |
| -------------------- | --------------------------------------------------- |
| backend offline      | safe offline status/output without crash            |
| health unhealthy     | safe unhealthy status/output without crash          |
| missing token        | safe auth/missing-token status/output without crash |
| invalid token        | safe auth failure status/output without crash       |
| WebSocket close      | safe disconnect/reconnect handling without crash    |
| malformed event      | safe parse/error status without crash               |
| protocol mismatch    | safe protocol error status without crash            |
| future_sequence      | safe error handling without crash                   |
| future_ack           | safe error handling without crash                   |
| idempotency_conflict | safe error handling without crash                   |

Each condition must update safe status/output without crashing.

## 15. Status Bar and Output Channel Verification

Verify UI is limited to:

- status bar item
- output channel

Fail if Sprint 3 adds dashboard/webview redesign.

Required status states:

- disabled
- disconnected
- connecting
- connected
- unhealthy
- auth failed
- workflow started
- event received
- ACK sent
- reconnecting
- resume complete
- error

Output channel may log only safe metadata:

- backend status
- workflowId
- sessionId
- event name
- sequence
- error code
- safe summary

Output channel must not log:

- token
- full payload body
- prompts
- provider keys
- connection strings
- terminal text
- tool payloads
- file proposal payloads

## 16. Extension Unit Test Verification

Run actual repo extension test commands after inspecting scripts.

Expected commands from plan:

```powershell
pnpm install
pnpm --filter roo-cline check-types
pnpm --filter roo-cline test
pnpm --filter roo-cline lint
pnpm --filter roo-cline bundle
```

Where practical, also run repo-level:

```powershell
pnpm check-types
pnpm test
pnpm lint
pnpm build
```

If commands differ, use actual repo scripts and document why.

Required unit test coverage:

- config defaults
- disabled activation behavior
- existing activation/command registration convention
- no second activation system
- no automatic health polling when disabled
- no WebSocket connection when disabled
- SecretStorage set/replace/clear
- missing token behavior
- health URL construction
- WebSocket URL construction
- auth header construction
- `workflow.start` envelope creation
- `event.ack` envelope creation
- `session.resume` envelope creation
- Ajv schema validation
- `connection.ready` parsing
- `workflow.started` parsing
- `workflow.status` parsing
- `error` envelope parsing
- ACK tracking
- resume payload generation
- duplicate replay suppression
- safe output formatting
- failure handling
- static boundary checks

Fail if core unit tests are missing or failing.

## 17. Backend Regression Verification

Run:

```powershell
dotnet test backend/ChromaAgentics.Backend.sln
```

Fail if Sprint 3 breaks backend tests without documented environmental cause.

## 18. Manual Smoke Test Verification

If safe, run backend:

```powershell
docker compose up -d --build backend
```

Then manually verify extension flow:

1. set token through Command Palette
2. enable `chromaAgentics.backend.enabled`
3. run backend health command
4. connect event stream
5. receive `connection.ready`
6. start smoke workflow
7. receive `workflow.started`
8. receive `workflow.status`
9. verify ACK output
10. disconnect
11. reconnect
12. verify `session.resume`
13. verify duplicate replay is not displayed twice

Record:

- commands/actions
- observed output/status
- event names
- sequences
- errors if any

## 19. Documentation Verification

Verify `docs/EXTENSION_BACKEND_BRIDGE.md` includes:

- settings
- command IDs
- SecretStorage behavior
- health checks
- WebSocket connection flow
- smoke workflow
- ACK behavior
- reconnect/resume behavior
- duplicate suppression
- security boundaries
- troubleshooting

Verify `docs/PHASE_02_SPRINT_03_REPORT.md` includes:

- Sprint 2 gate result
- files changed
- dependencies added
- command IDs
- settings added
- SecretStorage behavior
- tests run
- backend regression result
- extension validation result
- manual smoke result
- known gaps
- security notes
- Phase 2 completion recommendation
- whether README status changed
- activation convention confirmation
- no second activation system confirmation
- no auto-connect/polling/connection while disabled confirmation
- `CommandId` union unchanged or reason documented
- Ajv schema validation confirmation
- inbound parsing tests confirmation

Verify `docs/API_CONTRACT.md` includes Sprint 3 extension-client behavior without changing backend protocol `0.2`.

Verify `docs/GETTING_STARTED_BACKEND.md` includes extension bridge smoke steps and troubleshooting for:

- missing token
- invalid token
- backend offline
- backend unhealthy
- replay mismatch
- future_sequence
- idempotency_conflict

## 20. README Status Guard

Verify README was not updated to mark Phase 2 complete during Sprint 3 implementation.

Expected:

- README does not mark Phase 2 complete
- README does not falsely claim full extension bridge production readiness
- README does not mark later phases complete
- `docs/PHASE_02_SPRINT_03_REPORT.md` may recommend Phase 2 completion after validation

Fail if README status was prematurely updated.

## 21. Phase 2 Exit Evidence Check

Sprint 3 may recommend Phase 2 completion only if V&V proves:

- C#/.NET backend starts locally
- health endpoint exists
- extension connects to backend
- basic event streaming is demonstrated
- localhost binding/dev auth are documented
- backend health endpoint has automated test coverage

If all are proven, report:

```text
Phase 2 completion recommendation: YES, pending separate README/status update.
```

If any are missing, report:

```text
Phase 2 completion recommendation: NO.
```

Do not update README unless explicitly instructed.

## 22. Final Report Format

Output exactly:

```markdown
# Phase 2 Sprint 3 Verification & Validation Report

## Verdict

PASS / PASS WITH ISSUES / FAIL

## Executive Summary

Brief result summary.

## Validation Matrix

| Area                     |    Result | Evidence | Issues |
| ------------------------ | --------: | -------- | ------ |
| Sprint 2 gate            | PASS/FAIL | ...      | ...    |
| Canonical files          | PASS/FAIL | ...      | ...    |
| Scope discipline         | PASS/FAIL | ...      | ...    |
| Activation behavior      | PASS/FAIL | ...      | ...    |
| Settings                 | PASS/FAIL | ...      | ...    |
| Commands                 | PASS/FAIL | ...      | ...    |
| SecretStorage            | PASS/FAIL | ...      | ...    |
| Dependencies             | PASS/FAIL | ...      | ...    |
| Bridge module            | PASS/FAIL | ...      | ...    |
| Protocol 0.2             | PASS/FAIL | ...      | ...    |
| JSON schema validation   | PASS/FAIL | ...      | ...    |
| Session/replay handling  | PASS/FAIL | ...      | ...    |
| Runtime failure handling | PASS/FAIL | ...      | ...    |
| Status/output UX         | PASS/FAIL | ...      | ...    |
| Extension tests          | PASS/FAIL | ...      | ...    |
| Backend regression       | PASS/FAIL | ...      | ...    |
| Manual smoke             | PASS/FAIL | ...      | ...    |
| Docs                     | PASS/FAIL | ...      | ...    |
| README guard             | PASS/FAIL | ...      | ...    |
| Phase 2 exit evidence    | PASS/FAIL | ...      | ...    |

## Commands Run

List exact commands, pass/fail, and key output.

## Extension Behavior Results

Summarize settings, commands, SecretStorage, health, WebSocket, start, ACK, resume, and duplicate suppression.

## Backend Regression Results

Summarize backend gate and regression tests.

## Security Findings

List token/logging/CORS/scope-boundary issues.

## Missing or Failed Requirements

Use severity:

- Critical
- High
- Medium
- Low

## Required Fixes Before Approval

Concrete required changes.

## Deferred Work Confirmed

List correctly deferred features.

## Phase 2 Completion Recommendation

YES / NO

Explain whether all README Phase 2 exit criteria were proven.

## Final Recommendation

Approve, approve with fixes, or reject.
```

## 23. Scoring Rules

### PASS

All required extension bridge behavior, tests, docs, security boundaries, and smoke validation pass. Phase 2 completion may be recommended if exit evidence is complete.

### PASS WITH ISSUES

Core extension bridge works, but minor docs/test/report gaps remain. No security issues. No broken backend. No scope creep.

### FAIL

Any of the following:

- extension build fails
- backend regression fails without environmental cause
- bridge auto-connects while disabled
- token is stored outside SecretStorage
- token is logged/displayed
- WebSocket auth broken
- `workflow.start` broken
- ACK broken
- `session.resume` broken
- duplicate replay suppression broken
- schema validation missing or ignored
- bridge calls file/terminal/MCP/approval/tool execution APIs
- README marks Phase 2 complete during implementation
- dashboard/webview redesign added
- scope creep into MAF/Ollama/RAG/approval execution
- required docs missing

Critical failures override everything.
