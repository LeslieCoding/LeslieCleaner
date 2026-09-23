# CleanMyPC Next Batch Operation UX

## Contract

All multi-row pages provide select-all, invert, clear, filtered selection, selected count, total size, risk summary, pause, cancel and partial-result reporting. Selection is scoped to the current query unless the user explicitly chooses all results.

## Safety

Mixed-risk batches are grouped and confirmed by risk. One failed item does not become success; completed, skipped, failed and cancelled counts remain visible.

## Acceptance

Test virtualized lists, stale refresh, duplicate clicks, cancellation, partial failure, large selection and restore/undo.

## Implementation dossier

- IDs: `LCN-UX-BATCH-001..010`; a batch owns an immutable preview snapshot, item IDs, action, permission result, and cancellation token.
- Preview displays selected/eligible/protected/denied counts, estimated impact, backup state, and rollback availability.
- Execution reports per-item state and preserves failures for retry or history navigation; it never collapses partial failure into success.
- Duplicate clicks attach to the active operation instead of starting a second mutation.
- Undo applies only to committed items with verified backups and declares unavailable items explicitly.
- Tests cover large virtualized selection, stale rows, cancellation, partial denial, retry, history, and restore.
