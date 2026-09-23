# CleanMyPC Next AI Code Review Standard

Version: 1.0  
Status: Normative

## Review gates

- Verify dependency direction: Domain/Core has no UI, Windows API or provider dependency.
- Verify every public API has XML documentation and every asynchronous operation accepts cancellation.
- Verify destructive actions require backup, explicit confirmation, security-gate approval and post-action readback.
- Verify all paths are canonicalized and bounded before access; reparse points are rejected where traversal occurs.
- Verify UI collections are mutated on the dispatcher context and no synchronous I/O runs on the UI thread.
- Verify exceptions are classified, logged with stable fields and surfaced with actionable guidance.
- Verify tests cover success, denial, failure, cancellation, retry, duplicate invocation and rollback.

## Review output

Each review records changed files, risk level, evidence commands, unresolved limitations and a release decision. “Build passes” is not a behavioral acceptance result. Any unverified requirement remains `PendingEvidence`.

## Mandatory blockers

Credential leakage, silent destructive behavior, unbounded disk scanning, unchecked external paths, swallowed exceptions and tests that mutate real user data block merge.

## Review packet

- IDs: `LCN-REVIEW-001..012`; every review records requirement IDs, changed layers, risk, evidence commands, and remaining gaps.
- Review checks domain direction, async cancellation, bounded parallelism, UI dispatcher ownership, and path/registry canonicalization.
- For destructive changes, review requires proof of confirmation, authorization, backup/quarantine, read-back, history, and rollback.
- Findings are classified `Blocker`, `Required`, `Advisory`, or `PendingEvidence`; only the first two block merge.
- A reviewer reruns focused tests or records why the evidence is unavailable; copied historical output is not current evidence.
