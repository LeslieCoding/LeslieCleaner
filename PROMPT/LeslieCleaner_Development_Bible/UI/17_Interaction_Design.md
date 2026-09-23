# CleanMyPC Next Interaction Design

## Interaction contract

Every action exposes current state, progress, cancellation and final verification. Selection is never lost silently when data refreshes. Failed rows remain actionable and explain the next safe step.

## Destructive work

Confirmation shows scope, item count, logical/allocated bytes, risk, backup ID and rollback availability. Double-clicks and retries are idempotent or rejected with a clear state.

## Acceptance

Test keyboard and pointer paths, rapid repeated commands, cancellation, navigation during work, stale selections, partial failures, notifications and undo/restore.

## Implementation dossier

- IDs: `LCN-UI-INTERACTION-001..012`; commands declare enablement, confirmation, cancellation, elevation, and rollback behavior.
- Selection actions bind to stable finding IDs; stale selections are rejected and refreshed rather than acting on a reused row index.
- Dangerous flows show evidence, scope, risk, permissions, backup state, and undo availability before execution.
- Navigation during work keeps operation state observable and lets the user return to progress/history safely.
- Partial failure displays itemized outcomes and recovery commands, never a generic success message.
