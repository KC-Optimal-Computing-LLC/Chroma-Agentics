# ADR-0001: Phase 2 Protocol-Support Tables

## Status

Accepted for Phase 2 Sprint 2.

## Context

Sprint 2 needs durable WebSocket protocol behavior before full orchestration is
implemented. The backend must persist emitted protocol events, support reconnect
and replay, and track cumulative client acknowledgements while preserving the
extension/backend boundary.

The target architecture includes a larger long-term PostgreSQL model for
workflow steps, tool calls, approvals, provider config, RAG, and audit history.
Those tables are not needed for this sprint and would expand runtime scope.

## Decision

Add only these protocol-support tables:

- `Workspaces`
- `WorkflowExecutions`
- `WorkflowSessions`
- `ExecutionEvents`
- `EventAcknowledgements`

Use `WorkflowExecutions.NextSequence` as the per-workflow sequence allocator.
`PostgresEventStore.AppendEventAsync` locks the workflow row with
`SELECT ... FOR UPDATE`, reads `NextSequence`, assigns it to the new event,
increments `NextSequence`, inserts the event, and commits the transaction.

Use a filtered unique index for idempotency:

```text
unique (WorkflowId, Name, IdempotencyKey) where IdempotencyKey is not null
```

## Consequences

- Sequence numbers are monotonic, unique per workflow, and stable after
  persistence.
- Replay can use `ExecutionEvents` ordered by `(WorkflowId, Sequence)`.
- ACK state remains separate from approval or execution decisions.
- Sprint 2 does not create RAG, provider, approval execution, tool-call,
  patch-set, or pgvector tables.
- These tables are protocol infrastructure and do not replace the long-term
  target schema.
