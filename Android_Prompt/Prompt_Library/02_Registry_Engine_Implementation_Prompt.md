# LeslieCleaner Android Development Bible - 02_Registry_Engine_Implementation_Prompt

> Android Prompt ID: AP-PROMPT-LIBRARY-02-REGISTRY-ENGINE-IMPLEMENTATION-PROMPT-MD  
> Source Prompt: `Prompt_Library\02_Registry_Engine_Implementation_Prompt.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: Android storage, cache, package-data and scoped cleanup providers (SAF/MediaStore/PackageManager)  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **Android storage, cache, package-data and scoped cleanup providers (SAF/MediaStore/PackageManager)**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-PROMPT-LIBRARY-02-REGISTRY-ENGINE-IMPLEMENTATION-PROMPT-MD` in the Android traceability matrix.
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
# Registry Engine Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent together with `Modules/22_Registry_Cleaner.md` and `00_Execution_Contract.md`.

## Mission

Implement or repair registry scanning and cleanup for CleanMyPC Next. The engine must be precise, view-aware, recoverable, and permission-safe. A registry row is not actionable merely because it matches text; it needs rule evidence, risk assessment, policy approval, backup, and read-back verification.

## Mandatory registry identity

Every registry resource is identified by:

```text
Hive, PhysicalView, SubKeyPath, ValueName, ValueKind, CanonicalIdentity
```

Never use merged `HKCR` behavior when scanning or deleting. Open `HKCU\Software\Classes` and `HKLM\Software\Classes` explicitly with `Registry32` or `Registry64`. A 32-bit and 64-bit result are separate physical targets even when their display paths resemble each other.

## Scan policy

1. Start only from versioned rules and explicitly selected bounded branches.
2. Gather exact evidence: matching pattern, referenced file/package state, source view, owner/protection status, and risk reason.
3. Normalize case for matching but preserve actual stored casing for display and restore.
4. Classify findings as verified, candidate, protected, inaccessible, missing, stale, or denied.
5. Never enumerate the entire registry as a default scan path.

Rules must state hive/view applicability, path pattern, value pattern, evidence predicate, risk, default action, exclusions, and test fixture. A pattern without an exclusion for protected Microsoft/security paths cannot be enabled.

## Permission and protection policy

- Evaluate protection before opening writable handles.
- Windows Resource Protection, policy, security, integrity, boot, service, driver, and protected Microsoft targets are denied by default.
- HKLM and ACL-protected paths require typed elevation through the security gate; UAC refusal remains a visible denied state.
- Do not change ACLs merely to make deletion succeed unless a separately authorized repair feature owns that requirement.
- An access-denied result must identify hive, view, canonical path, action, and safe next action without exposing secrets.

## Backup and mutation protocol

For every key or value mutation:

1. Reopen the exact hive/view read-only and verify the precondition.
2. Capture a backup containing hive, view, key path, original value name/casing, kind, data, operation ID, timestamp, and integrity hash.
3. Verify the backup can be read before obtaining a writable handle.
4. Open the parent/key in the same physical view with the minimum writable scope.
5. Delete only the approved key/value; flush if supported.
6. Reopen read-only and read back the target with case-insensitive name matching.
7. If verification fails, attempt restore and return `RolledBack` or `Failed`—never `Committed`.

Repeated deletion of a verified missing target is idempotent and returns a truthful `NotFound/AlreadyAbsent` result, not an exception-driven failure.

## Logging

Emit `RegistryScan.Start`, `RegistryScan.Finding`, `RegistryDelete.Start`, `RegistryDelete.BackupVerified`, `RegistryDelete.Decision`, `RegistryDelete.ReadBack`, `RegistryDelete.Rollback`, and `RegistryDelete.Failure`.

Required fields: operation/requirement ID, hive, view, canonical path, value name/kind, rule ID, risk, policy decision, elevated state, backup hash, read-back result, duration, and failure kind. Do not log raw sensitive values.

## Tests

Add/maintain tests for:

1. HKCU/HKLM and Registry32/Registry64 physical separation.
2. Value-name casing capture and case-insensitive deletion/read-back.
3. Missing target and repeat invocation idempotence.
4. Protected policy denial before mutation.
5. Access denial and declined elevation guidance.
6. Corrupt or unverified backup refusal.
7. Read-back failure triggering restore.
8. Exact key/value scope; sibling keys/values remain intact.
9. Cancellation before mutation and between batch items.
10. Structured log fields for both success and failure.

## Handoff

Report the precise hive/view coverage, rules changed, permission boundary, backup/rollback evidence, tests and counts, plus any all-users/UAC Windows matrix still pending.

```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

