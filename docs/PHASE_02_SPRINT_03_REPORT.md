# Phase 2 Sprint 3 Report

## Sprint Goal

Build the smallest safe VS Code extension bridge proving the Roo-derived
extension can connect to the Sprint 2 backend and demonstrate protocol `0.2`
event streaming.

## Sprint 2 Gate Result

Passed before Sprint 3 implementation.

- `dotnet restore backend/ChromaAgentics.Backend.sln`: succeeded.
- `dotnet build backend/ChromaAgentics.Backend.sln`: succeeded with `0`
  warnings and `0` errors.
- `dotnet test backend/ChromaAgentics.Backend.sln`: succeeded with `46` tests
  passing.
- `docker compose config`: succeeded.
- Sprint 2 report and protocol schema artifacts exist.
- Sprint 2 documents durable `workflow.started` and `workflow.status` events,
  non-durable `connection.ready`, ACK status, resume-current status, and error
  envelopes.

## Final Scope

Delivered only the extension/backend bridge slice. The bridge performs health
checks, WebSocket connection, protocol `0.2` smoke workflow start, ACK, resume,
duplicate replay suppression, safe status output, and tests.

The bridge does not execute approvals, file edits, terminal commands, MCP/tool
actions, model calls, orchestration workflows, RAG, pgvector, dashboards,
LangGraph, or n8n.

## Files Changed

Created:

- `agents/phase-02-sprint-03-extension-bridge.agent.md`
- `apps/vscode-e2e/src/suite/backend-bridge.test.ts`
- `src/services/backend-bridge/*`
- `src/services/backend-bridge/__tests__/*`
- `docs/EXTENSION_BACKEND_BRIDGE.md`
- `docs/PHASE_02_SPRINT_03_REPORT.md`
- `prompts/phase-02-sprint-03-implementation.prompt.md`
- `prompts/phase-02-sprint-03-verification-validation.prompt.md`

Modified:

- `apps/vscode-e2e/src/suite/utils.ts`
- `apps/vscode-e2e/src/types/global.d.ts`
- `src/extension.ts`
- `src/package.json`
- `src/package.nls.json`
- `src/__mocks__/vscode.js`
- `src/__tests__/extension.spec.ts`
- `pnpm-lock.yaml`
- `docs/API_CONTRACT.md`
- `docs/GETTING_STARTED_BACKEND.md`

Sprint 3 prompt artifacts now use the `.prompt.md` naming convention as the
canonical path format.

README status was not changed during Sprint 3 implementation.

## Dependencies Added

- Runtime: `ws` `8.20.0`
- Dev: `@types/ws` `8.18.1`
- Dev: `ajv` `8.18.0`

`ws` is used because the backend WebSocket requires the custom
`X-Chroma-Dev-Token` header.

## Extension Command IDs

- `chromaAgentics.backend.setToken`
- `chromaAgentics.backend.clearToken`
- `chromaAgentics.backend.health`
- `chromaAgentics.backend.connect`
- `chromaAgentics.backend.startSmokeWorkflow`
- `chromaAgentics.backend.disconnect`

The existing Roo `CommandId` union was unchanged. Sprint 3 commands are
registered separately through the existing activation path with explicit
`chromaAgentics.backend.*` IDs.

## Settings Added

- `chromaAgentics.backend.enabled`
- `chromaAgentics.backend.url`
- `chromaAgentics.backend.connectionTimeoutMs`
- `chromaAgentics.backend.reconnect.enabled`
- `chromaAgentics.backend.reconnect.maxAttempts`
- `chromaAgentics.backend.reconnect.initialDelayMs`

Defaults match the approved Sprint 3 plan.

## SecretStorage Behavior

The backend development token is stored only in VS Code SecretStorage under
`chromaAgentics.backend.devToken`. Set/replace and clear commands use
SecretStorage directly. Tests cover store, replace, read, and clear behavior.

## Activation And Registration Validation

- Confirmed backend bridge commands use the existing extension activation and
  command registration convention.
- Confirmed no second activation system was introduced.
- Confirmed command registration and status bar creation occur on activation.
- Confirmed backend bridge does not auto-connect when disabled.
- Confirmed no health polling occurs when disabled.
- Confirmed no WebSocket connection occurs when disabled.
- Confirmed explicit user commands may run while automatic bridge behavior is
  disabled.

## Protocol And Test Coverage

Automated extension tests cover:

- Config defaults and disabled activation behavior.
- SecretStorage token lifecycle.
- Missing token behavior.
- Health URL and WebSocket URL construction.
- `X-Chroma-Dev-Token` header construction.
- `workflow.start`, `event.ack`, and `session.resume` envelope creation.
- Ajv validation of extension-created `workflow.start`, `event.ack`, and
  `session.resume` envelopes against protocol `0.2` JSON Schemas.
- Parsing behavior for `connection.ready`, `workflow.started`,
  `workflow.status`, and `error` envelopes.
- ACK sequence tracking.
- Resume payload generation.
- Duplicate replay suppression.
- Safe output formatting.
- Backend error handling.
- Static scope boundary checks.

Schema drift was not detected in Sprint 3 tests.

The focused Extension Development Host smoke uses a minimal test seam only:

- command-driven token setup through `chromaAgentics.backend.setToken`
- a non-user-facing `__chromaBackendBridgeTestHandle`
- read-only inspection helpers for session, logger, and status bar state

The seam is used only by E2E tests and does not expose token values, payload
bodies, prompts, provider secrets, terminal text, or tool payloads.

## E2E Harness Patch Summary

The first Extension Development Host smoke attempt failed before bridge
assertions ran because `apps/vscode-e2e/src/suite/utils.ts` pulled a runtime
dependency chain through `@roo-code/types`, which in this workspace resolution
path surfaced a broken `ai-sdk-provider-poe` export.

The fix was isolated to E2E support code:

- `apps/vscode-e2e/src/suite/utils.ts` keeps `RooCodeAPI` as a type-only import
  and replaces runtime event-name usage with explicit string constants and test
  casts.
- `apps/vscode-e2e/src/types/global.d.ts` adds test-only typing for the global
  bridge test handle.

No extension production runtime behavior was changed to resolve this import
blocker.

## Build And Test Commands

Backend gate:

```powershell
dotnet restore backend/ChromaAgentics.Backend.sln
dotnet build backend/ChromaAgentics.Backend.sln
dotnet test backend/ChromaAgentics.Backend.sln
docker compose config
```

Extension focused validation:

```powershell
pnpm install --lockfile-only --ignore-scripts
pnpm --filter roo-cline exec tsc --noEmit --pretty false
pnpm --filter roo-cline exec vitest run services/backend-bridge __tests__/extension.spec.ts --reporter=dot
pnpm --filter @roo-code/vscode-e2e check-types
pnpm --filter @roo-code/vscode-webview build
docker compose up -d --build backend
curl http://localhost:5127/health/ready
$env:TEST_FILE='backend-bridge.test'; pnpm --filter @roo-code/vscode-e2e test:run
```

Full extension validation:

```powershell
pnpm --filter roo-cline check-types
pnpm --filter roo-cline test
pnpm --filter roo-cline lint
pnpm --filter roo-cline bundle
```

Final command results are recorded at the end of this report after validation.

## Manual Smoke Result

A targeted Extension Development Host smoke was run in this terminal session
after bundling the extension, building the webview assets, and starting the
local Compose backend.

Final smoke command:

```powershell
docker compose up -d --build backend
curl http://localhost:5127/health/ready
$env:TEST_FILE='backend-bridge.test'; pnpm --filter @roo-code/vscode-e2e test:run
```

Backend status during smoke: `healthy` from `/health/ready` immediately before
the E2E run.

Token injection method: the smoke invokes the real
`chromaAgentics.backend.setToken` command in the Extension Development Host with
the dev token argument, which still stores the token through the normal
SecretStorage command path.

Recorded flow:

1. Enabled `chromaAgentics.backend.enabled` in the Extension Development Host
   test workspace.
2. Ran `chromaAgentics.backend.setToken` and stored the dev token in
   SecretStorage.
3. Ran `chromaAgentics.backend.health` and observed healthy backend status.
4. Ran `chromaAgentics.backend.connect` and observed `connection.ready`.
5. Ran `chromaAgentics.backend.startSmokeWorkflow` and observed
   `workflow.started`, `workflow.status`, and `event.ack`.
6. Ran `chromaAgentics.backend.disconnect`, reconnected, observed
   `session.resume`, and verified duplicate replay suppression.

The smoke passed through the real Extension Development Host in
`apps/vscode-e2e/src/suite/backend-bridge.test.ts`.

Observed bridge evidence:

- `connection.ready` received
- `workflow.start` sent
- `workflow.started` received
- `workflow.status` received
- `event.ack` sent after durable event processing
- disconnect and reconnect executed through real commands
- `session.resume` sent on reconnect
- duplicate replay suppressed without increasing processed-event count

Key output lines from the final rerun:

- `Running specific test file: backend-bridge.test.js`
- `✔ runs the backend bridge smoke flow through Extension Development Host commands (2249ms)`
- `1 passing (3s)`
- `Exit code: 0`

Non-blocking environment warnings were still present during the Extension Host
run: missing `.env.local`, VS Code mutex/proposed API warnings, missing
PostHog API key, and a textMate worker dynamic import warning. The smoke test
itself still passed cleanly.

## Security Notes

- The bridge logs only safe metadata.
- The token is never logged or displayed.
- WebSocket auth uses `X-Chroma-Dev-Token`; query-string token auth is not used
  by the extension bridge.
- Static tests confirm the bridge does not use file edit, terminal execution,
  MCP, approval, tool execution, dashboard, or webview APIs.
- README was not updated to mark Phase 2 complete.

## Known Gaps

- Session state is memory-only and is lost across extension host restarts.
- README remains intentionally unchanged until a separate status update is
  approved.

## Risks

- Development token auth remains local bootstrap auth only.
- The extension package still carries Roo package identity while Chroma-specific
  bridge settings use `chromaAgentics.backend.*`.
- The current workstation command environment uses Node `v25.9.0` while the repo
  expects Node `20.19.2`; validation commands report this as an engine warning.

## Phase 2 Completion Recommendation

YES, pending separate README/status update.

Evidence now proves the Phase 2 exit criteria used by Sprint 3 V&V:

- the C#/.NET backend starts locally
- health endpoints respond successfully
- the extension connects to the backend from a real Extension Development Host
- basic event streaming is demonstrated end to end
- localhost binding and dev-token auth are documented
- backend health coverage and regression tests are present

## Final Validation Results

Backend:

- `dotnet restore backend/ChromaAgentics.Backend.sln`: passed.
- `dotnet build backend/ChromaAgentics.Backend.sln`: passed with `0`
  warnings and `0` errors.
- `dotnet test backend/ChromaAgentics.Backend.sln`: passed with `46` tests.
- `docker compose config`: passed.
- `dotnet ef database update --project backend/src/ChromaAgentics.Backend --startup-project backend/src/ChromaAgentics.Backend`:
  initially failed while local PostgreSQL was offline; passed after
  `docker compose up -d --build backend` using the local Compose PostgreSQL
  connection string.
- Health smoke after Compose startup: `/health/live`, `/health/ready`, and
  `/health/dependencies` returned HTTP `200`.

Backend protocol smoke:

- `connection.ready` returned protocol `0.2` with `sequence: null`.
- `workflow.start` returned durable `workflow.started` sequence `1` and
  durable `workflow.status` sequence `2`.
- `event.ack` returned non-durable `ack.updated` for sequence `2`.
- Reconnect plus `session.resume` from sequence `1` replayed sequence `2`.
- Resume from latest returned non-durable `resume.current`.
- Resume with future sequence returned `future_sequence`.
- Duplicate idempotency retry returned original durable sequences `1,2`.
- Changed-payload idempotency retry returned `idempotency_conflict`.

Extension and repo validation:

- `pnpm install`: passed; reported the existing Node engine warning because the
  workstation is using Node `v25.9.0` while the repo expects `20.19.2`.
- `pnpm --filter roo-cline exec tsc --noEmit --pretty false`: passed.
- `pnpm --filter roo-cline exec vitest run services/backend-bridge __tests__/extension.spec.ts --reporter=dot`:
  passed with `29` focused tests.
- `pnpm --filter @roo-code/vscode-e2e check-types`: passed.
- `pnpm --filter @roo-code/vscode-webview build`: passed with chunk-size
  warnings only.
- `curl http://localhost:5127/health/ready`: returned `healthy` JSON
  immediately before the final focused smoke rerun.
- `$env:TEST_FILE='backend-bridge.test'; pnpm --filter @roo-code/vscode-e2e test:run`:
  passed with `1` targeted Extension Development Host smoke test covering token
  set, enable, health, connect, smoke workflow start, ACK, disconnect,
  reconnect, `session.resume`, and duplicate replay suppression. Key output
  lines were `Running specific test file: backend-bridge.test.js`,
  `✔ runs the backend bridge smoke flow through Extension Development Host commands (2249ms)`,
  `1 passing (3s)`, and `Exit code: 0`. Non-blocking warnings remained limited
  to missing `.env.local`, VS Code mutex/proposed API warnings, missing
  PostHog API key, and a textMate worker import warning.
- `pnpm --filter roo-cline check-types`: passed.
- `pnpm --filter roo-cline test`: passed with `5431` tests and `45` skipped.
- `pnpm --filter roo-cline lint`: passed.
- `pnpm --filter roo-cline bundle`: passed.
- `pnpm check-types`: passed.
- `pnpm test`: passed across `11` package tasks.
- `pnpm lint`: passed.
- `pnpm build`: failed in the pre-existing `@roo-code/web-evals#build` task
  because `apps/web-evals` could not resolve generated `packages/types`
  module exports such as `./api.js`, `./cli.js`, and `taskEventSchema`. The
  Sprint 3 extension bundle passed independently.
