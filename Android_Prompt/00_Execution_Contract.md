# LeslieCleaner Android Development Bible - 00_Execution_Contract

> Android Prompt ID: AP-00-EXECUTION-CONTRACT-MD  
> Source Prompt: `00_Execution_Contract.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: Android platform architecture, lifecycle, storage, permissions, testing and release governance  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **Android platform architecture, lifecycle, storage, permissions, testing and release governance**. The result must be useful on stock Android,
degrade safely on vendor-modified Android, and never require root, hidden APIs, bootloader unlock,
or an undocumented permission merely to complete a scan.

## 1A. LAEP execution contract

This document is executed through the LAEP cycle: Read -> Analyze -> Plan -> Implement -> Build ->
Test -> Debug -> Retest -> Regression -> Document -> Continue. Before editing, restore
`Android_App/AI_SPEC/24_PROJECT_STATE.md`, `25_PROGRESS.md`, `26_DECISION_LOG.md` and
`27_KNOWN_ISSUES.md`. Every change must identify requirement IDs, risk, rollback, commands, results
and remaining evidence. Device access is a gated operation: do not connect to or mutate a device
until the APK/package and local verification gates declared in PROJECT_STATE have passed.

## 2. Required architecture

- Kotlin, Jetpack Compose/Material 3 for presentation, and a lifecycle-aware ViewModel per screen.
- Clean boundaries: `app` (bootstrap/navigation), `feature-*` (UI/use cases), `domain` (typed
  policies and results), `data` (Room/DataStore/repositories), and `platform` (PackageManager,
  Storage Access Framework, MediaStore, UsageStats, WorkManager and optional ADB test adapter).
- Kotlin coroutines with structured concurrency. No GlobalScope, blocking calls on the main thread,
  or retained Activity/Context references in domain objects.
- Every platform operation returns a typed result with `success`, `failureKind`, `requiresUserAction`,
  `changedCount`, `verified`, `rollbackState`, and a correlation/operation ID.
- Build variants are `debug`, `benchmark`, and `release`; release must not include test endpoints,
  permissive network trust, or destructive mock fallbacks.

## 3. Android permissions and trust model

| Capability | Default | Required user action | Prohibited shortcut |
|---|---|---|---|
| App/package inventory | allowed through PackageManager | none beyond normal install | hidden package APIs |
| Shared files | scoped storage | SAF directory/file grant when needed | MANAGE_EXTERNAL_STORAGE by default |
| Media cleanup | MediaStore + recoverable delete | system confirmation where required | direct provider database writes |
| Usage/launch evidence | optional | Usage Access settings screen | accessibility abuse |
| Notifications | optional | POST_NOTIFICATIONS on API 33+ | silent background spam |
| Network/AI | opt-in | explicit provider and privacy consent | uploading raw paths, tokens, or content |
| ADB board tests | test-only | USB debugging and RSA approval | production dependency on ADB |

Permission denial, revocation, restricted profiles, work profiles and OEM policy changes are normal
typed outcomes. Do not retry in a loop. Explain the minimum user action and preserve all scan data.

## 4. Scan accuracy and target identity

All findings must carry a stable typed identity: package name plus user/profile, URI plus persistable
grant, MediaStore ID, or an app-owned database key. Display strings are never used as delete keys.
Normalize case only where the Android API is case-insensitive; preserve URI encoding and authority.
Deduplicate by physical identity, retain the strongest risk evidence, and record provider/source,
API level, profile, permission state and timestamp. A cached result is usable only when its provider
fingerprint and permission snapshot still match the current device.

## 5. Backup, mutation, read-back and rollback

No destructive operation is valid without a preflight snapshot or a platform-supported reversible
transaction. The sequence is mandatory:

1. Create an operation ID and durable manifest.
2. Resolve and re-check the exact target using the current profile/user and provider.
3. Capture metadata/content that Android legally permits, hash it, and verify the snapshot.
4. Ask for confirmation when the risk policy requires it.
5. Execute through the public Android API (PackageInstaller, MediaStore recoverable delete, SAF, or
   app-owned transaction).
6. Re-query the same provider and verify the expected absence/change; never infer success from a
   returned `Intent` alone.
7. On failed verification, stop further mutation and attempt rollback; report `Partial` or
   `VerificationFailed` rather than claiming success.

Irreversible operations are isolated behind an explicit second confirmation and are never included
in a bulk action by default.

## 6. Logging and diagnostics

Write structured JSONL events to the app-private files directory and expose an exportable diagnostic
bundle. Every scan/mutation records `TimestampUtc`, `OperationId`, `Feature`, `Provider`, `ApiLevel`,
`ProfileId` (redacted), `PermissionState`, `Decision`, `Result`, `Verified`, `FailureKind`, latency,
and counts. Never log raw personal paths, file contents, account identifiers, OAuth tokens, or ADB
serials without redaction. Use atomic append/rotation, bounded retention, crash-safe flush for
destructive events, and a self-diagnostic sink when the primary file is unavailable.

## 7. Lifecycle and concurrency

Cancellation is propagated from the UI and WorkManager. If cancellation occurs after mutation has
started, finish a bounded read-back/rollback attempt with a non-cancellable cleanup token, then
return `CancelledAfterMutation` with truthful verification state. Screen rotation must preserve the
operation ID and observe the same state rather than starting a second operation. A per-target or
per-profile mutex prevents duplicate taps and stale selections.

## 8. Testing and acceptance

- JVM unit tests for identity normalization, deduplication, policy, risk and typed failures.
- Robolectric/instrumentation tests for PackageManager, MediaStore, SAF grants, DataStore/Room and
  lifecycle restoration.
- Device tests on API 29, 30, 33 and 35 where available, including work profile and permission
  denial paths. ADB tests are read-only unless a disposable fixture and explicit test flag exist.
- Mutation tests must assert backup hash, exact target, post-operation provider read-back, rollback
  behavior, cancellation-after-mutation and durable log events.
- Performance tests must record cold/warm scan latency, allocations, battery impact and provider
  query counts; no full shared-storage walk is permitted when a scoped rule can locate the target.

Acceptance is not "the button returned." It requires `verified=true`, an inspectable operation log,
and a reproducible test artifact or an explicit `PendingEvidence` note for device-only behavior.

## 9. Implementation checklist

- [ ] Add/confirm requirement IDs `AP-00-EXECUTION-CONTRACT-MD` in the Android traceability matrix.
- [ ] Implement the domain contract and Android platform adapter separately.
- [ ] Add permission-denied, empty-result, cancellation and OEM-error branches.
- [ ] Add backup/hash/read-back/rollback tests for every mutation path.
- [ ] Add structured start/decision/result/failure logs and redaction tests.
- [ ] Add Compose semantics, TalkBack labels, large-font and dark-theme checks where UI applies.
- [ ] Record build, test, device/API, unresolved issue and evidence paths in `DEVELOPMENT_LOG.txt`.

## 10. AI execution prompt

Implement only the scope of this document. Before coding, inspect the current Android module and
traceability matrix. Do not invent Windows-only APIs. Keep changes small and reversible, add tests
before widening permissions, and report exact files, commands, test counts, device/API evidence,
known limitations and rollback behavior. If Android cannot legally or reliably provide a capability,
return a typed unsupported result and document the user-visible alternative.

## 11. Source prompt retained for traceability

The following is the source document that this Android adaptation translates. Its requirements may
be expanded for Android but must not be silently dropped:

<details>
<summary>Original source content</summary>

```markdown
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

```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

