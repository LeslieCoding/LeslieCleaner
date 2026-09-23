# LeslieCleaner Android Development Bible - 05_Cache_Rules_Implementation_Prompt

> Android Prompt ID: AP-PROMPT-LIBRARY-05-CACHE-RULES-IMPLEMENTATION-PROMPT-MD  
> Source Prompt: `Prompt_Library\05_Cache_Rules_Implementation_Prompt.md`  
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

- [ ] Add/confirm requirement IDs `AP-PROMPT-LIBRARY-05-CACHE-RULES-IMPLEMENTATION-PROMPT-MD` in the Android traceability matrix.
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
# Cache Rules and Smart Scan Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent with `Modules/20_Smart_Scan.md`, `Modules/27_Browser_Cleaner.md`, and `00_Execution_Contract.md`.

## Mission

Build a versioned Cache Rules system for Windows 10/11 that locates safe-to-clean data directly. It must cover Windows caches, browsers, developer tools, games, creative software, and AI clients without default full-volume scanning.

## Rule model

Every rule has `RuleId`, `Module`, `Provider`, `Category`, `Platform`, `PathTemplate`, `Include`, `Exclude`, `OwnershipPredicate`, `Risk`, `DefaultAction`, `RequiresProcessClosed`, `MinAppVersion`, `MaxAppVersion`, `RuleVersion`, `LastVerifiedUtc`, and `TestFixture`.

Rules are deterministic data, not scattered path literals. Rule changes require code review, fixture update, version bump, changelog entry, and a false-positive test.

## Provider families

Prioritize direct rules for:

1. Windows temp, logs, crash dumps, thumbnails, update/download caches, Delivery Optimization, shader caches, GPU/font caches, and recycle-bin metadata.
2. Chromium, Edge, Firefox, WebView, Arc, and other browser cache-only locations.
3. VS Code, Cursor, JetBrains, Visual Studio, Node package/cache tools, Python package/cache tools, Java tooling, Docker, WSL, and Hyper-V diagnostics.
4. Steam, Epic, Battle.net, Unity, Unreal, Adobe, Office, QQ, WeChat, and other provider-specific cache/log locations.
5. ChatGPT desktop, Claude desktop, AI editor, local-model, and AI-plugin caches only after profile ownership and category policy checks.

The rule must identify the exact category. “Application data” alone is not a safe category.

## Scan flow

`Load signed/versioned rules → Expand approved templates → Canonicalize → Check ownership/profile → Check lock/process policy → Measure → Exclude protected/session data → Deduplicate → Preview`.

The scan returns direct-location evidence, not merely a filename match. Missing or inaccessible locations are counted per rule and shown as partial coverage.

## Safety defaults

- Cache, temporary logs, crash reports, installer leftovers, and reproducible shader data may be candidates when rules verify ownership.
- Credentials, cookies, browser sessions, saved passwords, documents, project source, game saves, license files, and user-created content are excluded unless a separately confirmed feature owns them.
- Locked databases are skipped by default; do not force-close a browser/editor without a dedicated user-confirmed workflow.
- Network paths, reparse points crossing scope, and other-user profiles are denied by default.

## Outputs and UX

For each finding expose category, provider, rule ID/version, canonical location, estimated bytes, access state, risk, process/lock state, permission, and rollback availability. The UI separates “estimated reclaimable” from “verified freed.”

The preview explains why an item is included and why excluded data is protected. A user can exclude a rule/category for this run; persistent exclusions are versioned settings with recovery/reset behavior.

## Tests and performance

Test template expansion, environment variables, missing roots, path traversal, reparse points, profile ownership, lock handling, exclusions, duplicate rules, stale rules, cancellation, and post-clean verification. Benchmark per-provider rule groups; cache scan is bounded by direct locations and must not invoke deep scan silently.

## Handoff

Report providers/rules added, safe default scope, exclusions, fixture coverage, benchmark evidence, rule version, and providers still `PendingEvidence`.

```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

