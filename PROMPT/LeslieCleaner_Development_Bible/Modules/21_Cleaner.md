# CleanMyPC Next Cleaner Module

## Objective

Clean only explicitly selected, rule-approved targets through reversible transactions.

## Execution contract

Analyze first, show evidence and risk, create a verified backup/quarantine record, then execute with cancellation and bounded concurrency. Never delete a protected or ambiguous item. Every item ends in `Cleaned`, `Skipped`, `Failed`, `Cancelled` or `Verified` state.

## Safety

Canonicalize paths, reject reparse escapes, preserve allowlists, isolate files by default and verify absence or expected state after execution. A failure never contributes to freed-byte totals.

## Acceptance

Test success, access denial, in-use files, cancellation, duplicate invocation, corrupt backup, rollback and accurate byte/item accounting.

## Implementation dossier

- IDs: `LCN-CLEAN-ENGINE-001..010`; inputs are validated findings from a current preview token only.
- Pipeline: validate → classify → confirm → authorize → quarantine/backup → execute → read-back → history.
- Output includes per-item status, operation ID, bytes, backup manifest, and rollback availability.
- Protected paths and machine resources require an explicit elevation result.
- Logs distinguish sharing violation, access denial, missing target, backup corruption, and verification failure.
- Tests cover idempotent repeated requests, cancellation between items, corrupt backup, partial rollback, and stale previews.
