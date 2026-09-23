# CleanMyPC Next Undo and Restore

## Objective

Make reversible operations discoverable while being honest about irreversible work.

## Contract

Undo references a verified backup or quarantine record, displays scope and expiry, and revalidates the record before restore. Registry/file/database restores use explicit conflict policy. Secure shred and recycle-bin purge are marked irreversible before execution.

## Acceptance

Test undo success, missing/corrupt backup, conflict policies, expiry, cancellation, repeated undo and irreversible-operation disclosure.

## Implementation dossier

- IDs: `LCN-UX-UNDO-001..010`; undo is bound to operation ID, verified backup manifest, resource identity, expiry, and conflict policy.
- UI distinguishes undo available, already restored, expired, unsupported, partial, and failed; irreversible actions are declared before execution.
- Restore validates backup integrity and current target conflict before writing; it is safe to retry after interruption.
- Partial rollback itemizes restored, skipped, conflicted, and missing resources with recovery guidance.
- Tests cover successful undo, corrupt/missing backup, conflict policy, expiry, cancellation, duplicate undo, and irreversibility disclosure.
