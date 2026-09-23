# CleanMyPC Next Execution Contract

Version: 2.0
Status: Normative
Priority: Critical

This document is the execution contract for every developer and AI agent working on CleanMyPC Next. It converts a feature description into an auditable implementation task. A document may describe intent; this contract defines what must exist before a requirement can be marked complete.

## 1. Normative language

The words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** are normative. If this document conflicts with a module specification, the stricter safety rule wins. If two requirements conflict, the implementation MUST stop and report the conflict instead of choosing silently.

## 2. Requirement identity

Every requirement receives a stable ID with this form:

`LCN-{AREA}-{MODULE}-{NNN}`

Examples: `LCN-RES-RESIDUAL-001`, `LCN-REG-DELETE-014`, `LCN-UX-UNDO-003`.

Each ID MUST have these fields:

| Field | Required meaning |
|---|---|
| Objective | User-visible result in one sentence |
| Scope | Exact files, registry hives, providers, or UI surfaces included |
| Non-scope | Explicitly excluded behavior |
| Inputs | Typed input and validation rules |
| Evidence | Data used to make the decision and its source stage |
| Permission | User/elevation/security-gate requirement |
| State | `Implemented`, `Partial`, `PendingEvidence`, or `NotImplemented` |
| Source | Production file and public interface |
| Tests | Deterministic test names and fixture boundaries |
| Acceptance | Reproducible command and expected result |
| Rollback | Backup, quarantine, or recovery operation |

No requirement is complete if any required field is missing.

## 3. AI intake protocol

Before changing code, the AI MUST:

1. Read the relevant Bible file, this contract, the current implementation, and existing tests.
2. Search for the requirement ID and list every existing implementation and limitation.
3. Identify the trust boundary: UI, provider, plugin, Windows API, file system, registry, or update package.
4. State the smallest responsible layer for the change.
5. Write the acceptance cases before editing production code.
6. Check whether the operation can affect real user data; if yes, require an isolated fixture or an explicit Windows acceptance command.
7. Preserve unrelated user changes and avoid speculative rewrites.

The AI MUST NOT invent a new abstraction when an existing platform or domain interface already expresses the behavior. It MUST NOT use a UI event handler as the source of business truth.

## 4. Required implementation packet

Every change MUST produce an implementation packet containing:

- Requirement IDs and a one-line change summary.
- Affected source files and why each layer changes.
- Permission and elevation decision.
- Data-flow description from input to result.
- Failure and cancellation behavior.
- Backup/quarantine and rollback behavior for destructive work.
- Structured log event names and stable fields.
- Focused tests, regression command, and expected counts.
- Performance measurement or an explicit reason it is not applicable.
- Known limitations and the next executable action.

## 5. Scan and retrieval contract

All discovery modules MUST use staged, bounded retrieval:

1. **Identity stage**: installed database, Appx identity, ProductCode, UpgradeCode, package ID, or provider manifest.
2. **Rule stage**: signed or versioned known-location rules.
3. **Evidence stage**: uninstall logs, file metadata, registry values, publisher, signature, and timestamps.
4. **Index stage**: local file/index database with explicit freshness and scope.
5. **Deep stage**: opt-in, bounded roots only, cancellable, reparse-point aware.

The default path MUST NOT recurse across an entire volume. Every candidate MUST include source, confidence, canonical identity, size estimate, access state, and explanation. Duplicate candidates are merged case-insensitively while retaining the highest-confidence evidence and all contributing sources.

Query matching MUST normalize Unicode FormKC, trim and collapse whitespace, compare exact identity before prefix/contains matches, and reject a conflicting product identity. A publisher-only match MUST NOT override a supplied product name.

## 6. Destructive operation state machine

Every delete, uninstall, registry mutation, shred, service change, or task change follows:

`Discovered → Classified → Previewed → Confirmed → Authorized → BackedUp/Quarantined → Executing → ReadBackVerified → Committed`

Allowed terminal states are `Committed`, `RolledBack`, `Denied`, `Cancelled`, `TimedOut`, and `Failed`. An operation MUST be idempotent: repeating it after a successful commit MUST report the target as already absent, not fabricate a new failure.

The operation MUST stop before execution when the target is protected, the path cannot be canonicalized, the backup cannot be verified, the permission gate denies access, or the precondition changed.

## 7. Permission and elevation contract

Permissions are evaluated by `(subject, resource, action, scope, expiry)`. The default is deny. The UI MAY request elevation, but only the operation service and security gate can authorize the action.

An elevated helper MUST receive a minimal, typed request and return a typed result. It MUST NOT accept arbitrary command-line text, inherit unbounded environment state, or broaden a path beyond the approved scope. The original page, selection, operation ID, and cancellation intent MUST survive a UAC restart.

## 8. Backup, quarantine, and rollback

Backups MUST be created before destructive mutation and verified by size/hash/metadata appropriate to the resource. Registry backups MUST include hive, view, key path, value name, value kind, and original casing. File quarantine MUST preserve relative identity, timestamps where possible, and an operation manifest.

Rollback MUST be explicit, auditable, and safe to repeat. A failed read-back verification MUST trigger rollback when possible and MUST leave the operation in a non-committed state. If rollback is partial, the result MUST identify every item requiring manual recovery.

## 9. Logging contract

Every operation emits `Start`, `Decision`, `Progress` where useful, `Finish`, and `Failure` or `Cancellation`. Required fields include `OperationId`, `RequirementId`, `Module`, `Stage`, `ResourceKind`, `CanonicalPath`, `Permission`, `Elevated`, `Confidence`, `ItemCount`, `SizeBytes`, `DurationMs`, and `FailureKind`.

Logs MUST be structured UTF-8, bounded, flushed on shutdown with a finite timeout, and redacted before persistence. Secrets, cookies, tokens, document contents, and full personal paths MUST NOT appear in provider prompts or diagnostics.

## 10. Concurrency and cancellation

Async APIs MUST accept a `CancellationToken`. Cancellation is cooperative and checked between items and before every destructive step. Shared collections use a documented ownership model; UI-bound collections are updated on the dispatcher. Parallelism is bounded by the resource type and MUST NOT multiply recursively.

Retry policies MUST classify transient I/O, sharing violations, access denial, invalid input, and permanent corruption separately. Retries require bounded count, jittered delay where appropriate, and a log entry for every exhausted retry.

## 11. Test contract

Each scanner tests: exact hit, prefix hit, false positive, conflicting identity, duplicate result, stale index, inaccessible item, reparse point, cancellation, and deep-scan refusal. Each destructive module tests: success, missing target, access denied, protected target, in-use target, backup failure, corrupted backup, duplicate invocation, cancellation, timeout, read-back failure, rollback, and partial rollback.

Tests MUST use isolated temporary paths, synthetic registry hives or uniquely namespaced test keys, local HTTP fixtures, and deterministic clocks where timing is asserted. Production paths, real user profiles, and unbounded volume scans are forbidden in ordinary tests.

## 12. Performance contract

Benchmarks record cold and warm runs, sample size, P50/P95/max, item count, bytes, CPU, private memory, allocations, and failure count. Rule stages have a fixed budget; deep scans have a separate budget and are never hidden inside a rule-stage measurement.

UI commands acknowledge within 100 ms, page transitions within 300 ms, and no synchronous file, registry, process, or network I/O runs on the dispatcher thread. A regression over 15% requires a diagnosis or a documented waiver.

## 13. UI and UX contract

The UI MUST display estimated versus verified bytes separately, identify evidence and confidence, show the required permission, and expose a rollback/history action for destructive work. Loading, empty, denied, partial, failed, cancelled, and completed states are distinct.

Every keyboard action has a visible focus state. Dangerous actions require a confirmation that names the scope, item count, estimated impact, backup state, and rollback path. Error text MUST tell the user what happened and what action is safe next.

## 14. Code review gates

Review MUST reject unbounded scans, silent deletion, swallowed exceptions, unchecked external paths, provider output used as authority, missing cancellation, missing read-back verification, untested permission denial, and claims of completion based only on compilation.

The review record lists changed files, risk, tests run, acceptance commands, unresolved limitations, and release decision. `PendingEvidence` is the correct status when an implementation exists without reproducible evidence.

## 15. Release gate

Release requires: clean restore/build, focused tests, ordinary regression, installer compile, install/upgrade/uninstall smoke, integrity baseline verification, rollback smoke, log export check, and a documented Windows version/architecture matrix. Unsigned or tampered packages, data-loss defects, silent failures, and unbounded scans block release.

## 16. AI response template

When asked to implement a requirement, the AI returns this structure before and after coding:

```text
Requirement: LCN-AREA-MODULE-NNN
Current state: Implemented | Partial | PendingEvidence | NotImplemented
Scope / non-scope:
Evidence sources:
Permission boundary:
Planned files:
Acceptance cases:
Rollback plan:
Implementation result:
Tests and exact counts:
Known limitations:
Next executable action:
```

The final response MUST distinguish code changed, tests passed, tests not run, and external evidence unavailable. It MUST NOT imply that an unconnected device, unsigned package, or unverified provider was tested successfully.
