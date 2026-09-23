# Cleaner Engine Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent together with `Modules/20_Smart_Scan.md`, `Modules/21_Cleaner.md`, and `00_Execution_Contract.md`.

## Mission

Implement or repair the common Cleaner Engine. It turns reviewed scan findings into recoverable actions. It must be the only execution path shared by Smart Scan, residual cleanup, browser/cache cleaning, and future rule modules; scanners never delete directly.

## Inputs and validation

Accept only a typed operation request containing current preview token, stable finding IDs, canonical resource identities, requested action, confirmation evidence, security decision, and cancellation token. Reject arbitrary paths, stale previews, duplicate operation IDs, mismatched module ownership, and findings whose preconditions changed after scan.

For every item, determine resource kind: file, directory, registry key, registry value, provider-owned object, or unsupported. The action policy determines quarantine, backup-and-delete, provider action, or refusal. Unsupported and protected resources are returned as typed skipped items.

## Required state machine

```text
ValidateRequest → RevalidateTarget → Authorize → Backup/Quarantine
→ Execute → ReadBackVerify → CommitHistory → Notify
```

Every item has its own result; a batch also has a summary. The summary is `Partial` when any item is denied, skipped, failed, cancelled, or rolled back. Do not report total requested bytes as freed bytes; report estimated, attempted, quarantined, and verified freed bytes separately.

## File and directory policy

- Canonicalize target and verify it remains under the approved root after any reparse-point-sensitive step.
- Quarantine by default when rollback is supported. Preserve an operation manifest with original identity, destination, size, timestamps, hash where practical, and restore policy.
- Do not recursively expand an arbitrary directory during execution. Enumerate only the approved canonical tree, with cancellation between items and protection/reparse checks.
- Sharing violation, access denied, missing target, protected target, and changed precondition are separate outcomes.
- A missing target after a verified earlier commit is idempotent, not an error.

## Registry policy

Delegate registry mutations to the Registry Engine. Supply explicit hive/view/key/value identity and require a verified backup plus read-back. Do not use merged HKCR or write handles selected by UI text.

## Rollback policy

Rollback is operation-ID based. It verifies backup/quarantine integrity, detects current-target conflicts, restores each item independently, and reports restored/skipped/conflicted/missing states. A failed rollback never removes the original operation history.

## Logging and history

Emit `Clean.Start`, `Clean.Decision`, `Clean.ItemStart`, `Clean.ItemFinish`, `Clean.ReadBack`, `Clean.Rollback`, `Clean.Cancelled`, and `Clean.Failure` with operation/requirement IDs. Store module, resource kind, canonical identity, estimated/verified bytes, permission/elevation, backup reference, duration, and failure kind. Redact diagnostic paths as required.

## Tests

Cover file quarantine, directory manifest, registry delegation, denied/protected/missing/in-use targets, stale preview, duplicate request, cancellation before and during batch, backup corruption, read-back failure, partial rollback, repeated rollback, and byte accounting. Ensure all fixtures stay inside isolated test directories/keys.

## Performance and UI

Progress is throttled, cancellation remains responsive, and UI collection updates occur on the dispatcher only. Large batches are streamed or paged; logging/backups must not cause unbounded memory growth. Show per-item recovery guidance and a history/undo route when applicable.

## Handoff

Report modules using the engine, state transitions, backup/quarantine behavior, read-back evidence, rollback results, test counts, performance measurements, and remaining privileged Windows evidence.
