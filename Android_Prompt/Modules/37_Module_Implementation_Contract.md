# LeslieCleaner Android Development Bible - 37_Module_Implementation_Contract

> Android Prompt ID: AP-MODULES-37-MODULE-IMPLEMENTATION-CONTRACT-MD  
> Source Prompt: `Modules\37_Module_Implementation_Contract.md`  
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

- [ ] Add/confirm requirement IDs `AP-MODULES-37-MODULE-IMPLEMENTATION-CONTRACT-MD` in the Android traceability matrix.
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
# Module Implementation Contract

Version: 2.0  
Status: Normative

This contract is shared by every file in `Modules/20–36`. Each module file supplies domain-specific rules; this document supplies the implementation depth that every module must meet.

## Module dossier

Every module MUST maintain a dossier with:

1. Objective and user stories.
2. Requirement IDs and priority.
3. Inputs, outputs, and domain models.
4. Discovery providers and source precedence.
5. Permission matrix.
6. Rule/configuration schema and versioning.
7. Pipeline stages and cancellation points.
8. Safety classification and protected targets.
9. Backup/quarantine/rollback behavior.
10. Logs, history records, notifications, and metrics.
11. Unit, integration, UI, performance, and release tests.
12. Known limitations and recovery instructions.

## Provider precedence

Providers are ordered from authoritative to heuristic. The module MUST retain the provider name and timestamp on each finding. A lower-confidence provider can add evidence but cannot silently downgrade a higher-confidence classification.

Provider failures are isolated. If one provider is unavailable, the result is marked partial and the user sees the missing source; the module does not claim a complete scan.

## Rule schema

Rules are versioned records with `RuleId`, `Module`, `Provider`, `Platform`, `Scope`, `Pattern`, `Risk`, `DefaultAction`, `MinConfidence`, `Exclusions`, `IntroducedVersion`, `LastVerifiedUtc`, and `SignatureStatus`.

Rules MUST be deterministic, reviewable, and testable without a live user profile. A rule that can match a protected operating-system path MUST include an explicit negative test.

## Result model

Every finding contains:

```text
FindingId, CanonicalIdentity, DisplayPath, Kind, Source, Evidence,
Confidence, Risk, SizeEstimate, AccessState, CanAct, RequiresElevation,
BackupRequired, RollbackAvailable, RuleVersion, CreatedUtc
```

The module MUST distinguish `NotFound`, `Inaccessible`, `Protected`, `Stale`, `Candidate`, and `Verified`. An inaccessible item is never silently treated as absent.

## Execution pipeline

`ValidateInput → Discover → Normalize → Deduplicate → Classify → Preview → Confirm → Authorize → Backup/Quarantine → Execute → ReadBack → PersistHistory → Notify`.

Each stage has a cancellation checkpoint and structured timing. Execution accepts only findings produced by the current operation or a signed, validated preview token; arbitrary paths supplied by the UI are rejected.

## Permission matrix

| Action | Standard user | Elevated helper | Blocked by default |
|---|---|---|---|
| Read user-scoped data | Yes, within scope | Optional | Outside approved roots |
| Read machine-scoped data | Best effort | Yes | Protected/system secrets |
| Move to quarantine | Yes if destination is writable | Yes | Protected artifacts |
| Delete registry or machine files | No unless policy permits | Yes with gate | WRP/security/integrity targets |
| Stop service/task/process | No by default | Explicit confirmation | Security and OS-critical targets |

## Acceptance matrix

Each module MUST have fixtures for clean machine, known leftovers, stale data, duplicate results, access denial, locked resource, protected resource, cancellation, and rollback. Test reports include counts, timings, environment, and artifact paths.

## Module-specific expansion

The following files MUST add their own acceptance details on top of this contract:

- `20_Smart_Scan`: category totals, estimated/verified bytes, and scan freshness.
- `21_Cleaner`: quarantine manifest, read-back, and partial failure presentation.
- `22_Registry_Cleaner`: physical 32/64-bit views, hive identity, ACL/UAC, and value casing.
- `23_Uninstaller`: provider selection, exit-code policy, process lifetime, and residual handoff.
- `24_Residual_Engine`: identity scoring, known rules, install logs, index evidence, and bounded deep scan.
- `25_Startup_Manager`: persistence locations, signature, delayed startup, and reversible disable.
- `26_Privacy_Guard`: browser/profile scope, cookie/session safety, and consent boundaries.
- `27_Browser_Cleaner`: provider version, locked databases, cache-only default, and browser restart warning.
- `28_File_Shredder`: algorithm, overwrite limits, SSD caveat, and verification result.
- `29_Duplicate_Finder`: hash tiers, collision policy, protected originals, and user selection.
- `30_Large_File_Analyzer`: index freshness, sparse files, hard links, and paging.
- `31_Service_Manager`: service dependencies, recovery settings, and boot-critical refusal.
- `32_Driver_Manager`: package identity, signed driver evidence, rollback and reboot requirement.
- `33_Task_Scheduler`: task XML identity, trigger preservation, and elevation boundary.
- `34_Extension_Manager`: host/profile ownership, signature, and reversible disable.
- `35_AI_Assistant`: evidence-only context, redaction, typed response, and no execution authority.
- `36_Settings`: schema migration, atomic writes, corrupt-config recovery, and reset behavior.

```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

