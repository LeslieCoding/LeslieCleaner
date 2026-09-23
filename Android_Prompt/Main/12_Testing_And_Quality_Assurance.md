# LeslieCleaner Android Development Bible - 12_Testing_And_Quality_Assurance

> Android Prompt ID: AP-MAIN-12-TESTING-AND-QUALITY-ASSURANCE-MD  
> Source Prompt: `Main\12_Testing_And_Quality_Assurance.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: On-device/local-first advisory AI, redaction, network opt-in, structured output validation and safe action delegation  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **On-device/local-first advisory AI, redaction, network opt-in, structured output validation and safe action delegation**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-MAIN-12-TESTING-AND-QUALITY-ASSURANCE-MD` in the Android traceability matrix.
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
# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Testing And Quality Assurance Specification


Priority:

★★★★★


---

# Purpose


This document defines the complete quality assurance system for LeslieCleaner.


The goal:


Ensure LeslieCleaner reaches commercial software quality.


Testing covers:


Functional Testing

↓

Security Testing

↓

Performance Testing

↓

Compatibility Testing

↓

Stability Testing

↓

Release Verification



---

# Quality Standard


LeslieCleaner must satisfy:


✓ Stable operation

✓ No critical crash

✓ No data loss

✓ Safe cleanup

✓ Fast response

✓ Low memory usage

✓ Professional user experience



---

# Testing Strategy


Testing pyramid:


```

              UI Testing


                 ▲


          Integration Testing


                 ▲


          Service Testing


                 ▲


          Unit Testing


```



---

# Testing Environment


## Operating Systems


Required:

---

# Hardware Testing


Minimum:


CPU:

Intel i3 / equivalent


Memory:

8GB RAM


Storage:

SSD recommended



---

Recommended:


CPU:

Intel i5/i7


Memory:

16GB+



---

# Test Build Types


## Debug Build


Purpose:


Development debugging.



---

## Release Build


Purpose:


Final user testing.


All performance tests must use Release Build.



---

# Test Phase Overview


```

Phase 1

Unit Testing


↓

Phase 2

Module Testing


↓

Phase 3

Integration Testing


↓

Phase 4

Performance Testing


↓

Phase 5

Security Testing


↓

Phase 6

Final Acceptance Testing


```



---

# Phase 1

# Unit Testing Standard


## Purpose


Verify individual components.



---

# Testing Framework


Recommended:


xUnit


or


NUnit



---

# Unit Test Rules


Every important class requires tests.



Example:


```
CleanerServiceTests


RegistryServiceTests


StartupServiceTests


```



---

# Unit Test Coverage Target


Core modules:


≥80%



Critical security modules:


≥90%



---

# Required Unit Test Modules



## Cleaner Engine


Test:


```

Scan rules

↓

File detection

↓

Size calculation

↓

Cleanup result

```



---

## Registry Engine


Test:


```

Registry detection

↓

Risk calculation

↓

Backup creation

↓

Restore


```



---

## Fingerprint Engine


Test:


```

Application matching

↓

Path detection

↓

Confidence calculation


```



---

## Startup Analyzer


Test:


```

Microsoft classification

↓

User application classification

↓

Impact calculation


```



---

## Database Layer


Test:


```

Insert

↓

Query

↓

Update

↓

Delete

↓

Migration


```



---

# Boundary Testing


Every module must test:


Normal input


↓

Empty input


↓

Invalid input


↓

Extreme input



---

Example:


File Scanner:


Test:


```

0 files


100 files


1 million files


```



---

# Exception Testing


Verify:


Permission denied


↓

File locked


↓

Registry inaccessible


↓

Database failure


↓

Network failure



---

# Phase 2

# Module Testing


Each feature module requires independent testing.



---

# System Cleaner Testing



## Test Cases



### Case 1


Scan temporary files.


Expected:


Detect unnecessary files.



---

### Case 2


Clean selected files.


Expected:


Files removed successfully.



---

### Case 3


Locked file.


Expected:


Skip safely and report.



---

### Case 4


Protected system directory.


Expected:


Cannot delete.



---

# Registry Cleaner Testing


## Required Tests



### Registry Scan


Verify:


Correct detection.



---

### Registry Backup


Verify:


Backup created before modification.



---

### Registry Restore


Verify:


Original state restored.



---

# Critical Requirement


Registry cleaner must never:


Delete essential   keys.



---

# Uninstaller Testing


## Test Applications


Include:


Small software


↓

Large software


↓

Portable software


↓

Microsoft Store application


↓

System component



---

# Uninstall Test Flow


```

Detect Application


↓

Analyze Fingerprint


↓

Create Backup


↓

Execute Official Uninstall


↓

Scan Residual


↓

Remove Residual


↓

Verify


```



---

# Residual Removal Performance Test


Important requirement:


The weakness of traditional cleaners:


Slow residual scanning.



---

# Test Requirement


Compare:


Traditional full disk scanning


VS


LeslieCleaner fingerprint-based scanning



---

# Target Result


Residual scan:


<30 seconds



---

# Startup Manager Testing


## Critical Safety Test


Must verify:


Microsoft startup items separated.



---

# Test Data


Include:


  Defender


↓

  Update


↓

System Services


↓

Third-party software



---

# Expected Result


Microsoft:


Protected category.



User Software:


Optimization category.



---

# Batch Operation Testing


All batch modules must test:


```

Select All


↓

Select None


↓

Invert Selection


↓

Batch Execute


```



---

# File Shredder Testing


Test:


Small file


↓

Large file


↓

Locked file


↓

Protected file



---

# Verification:


Destroyed file cannot be recovered through normal methods.



---

# Browser Cleaner Testing


Browsers:


```

Chrome

Edge

Firefox

Brave


```



---

# Test Data:


Cache


↓

History


↓

Cookies


↓

Session



---

# Safety Requirement:


Do not delete important login information without user confirmation.



---

# Privacy Cleaner Testing


Test:


Recent files


↓

Application traces


↓

Temporary records


↓

Browser traces



---

# AI Assistant Testing


Verify:


AI can:


Analyze


↓

Recommend


↓

Explain



---

# AI Restriction Test


Verify:


AI cannot:


Automatically delete


↓

Modify registry


↓

Disable security


without confirmation.



---

# Phase 3

# Integration Testing


Purpose:


Verify modules work together.



---

# Integration Scenarios



## Scenario 1


Complete cleanup workflow:


```

Scan


↓

Select


↓

Backup


↓

Clean


↓

Verify


```



---

## Scenario 2


Complete uninstall workflow:


```

Select Application


↓

Analyze


↓

Uninstall


↓

Residual Cleanup


↓

Report


```



---

## Scenario 3


Startup optimization:


```

Scan Startup


↓

Classify


↓

Disable User Startup


↓

Restore


```
---

# Phase 4

# Performance Testing


## Purpose


Ensure LeslieCleaner provides:

Fast scanning

↓

Low resource usage

↓

Smooth operation



---

# Performance Testing Environment


Use:


Release Build


+

Clean   Environment


+

Real User Environment



---

# Performance Metrics


Monitor:


CPU Usage


↓

Memory Usage


↓

Disk IO


↓

Thread Count


↓

Startup Time


↓

Response Time



---

# Application Startup Performance Test


## Requirement


Measure:


From application launch


to


Main Dashboard Ready.



---

# Target


Cold Start:


<3 seconds



Warm Start:


<2 seconds



---

# Memory Usage Test


## Idle State


Application opened without operation.



Target:


```
<120MB RAM

```



---

# Normal Operation


During scanning:


Target:


```
<300MB RAM

```



---

# Long Running Stability Test


Run:


24 hours


+

48 hours



Observe:


Memory growth


↓

Thread increase


↓

Handle leak


↓

Performance degradation



---

# Memory Leak Detection


Use:


  Performance Monitor


+

Visual Studio Diagnostic Tools



---

# Check:


Private Memory


↓

Managed Heap


↓

GC Collection


↓

Handle Count



---

# Memory Leak Criteria


Fail if:


Memory continuously increases without release.



---

# File Scanner Stress Test


## Test Environment


Create:


```

100,000 files


500,000 files


1,000,000 files


```



---

# Verify:


Scan completion


↓

Memory usage


↓

UI response


↓

Accuracy



---

# Large File Test


Create:


```

1GB

10GB

50GB


```


files.



---

# Verify:


Scanner does not:


Load complete file into memory.



---

# Duplicate Detection Performance Test


Generate:


Thousands of duplicated files.



Verify:


Hash calculation speed.



---

# Hash Optimization Requirement


Must use:


Small file:


Full Hash



Large file:


Partial Hash


↓

Full verification



---

# Database Performance Test


Test database:


Small data:


10,000 records



Large data:


1,000,000 records



---

# Verify:


Query speed


↓

Insert speed


↓

Migration speed


↓

Backup speed



---

# Database Performance Target


Normal query:


<50ms



Large query:


<500ms



---

# Registry Performance Test


Test registry size:


Small registry


↓

Large registry



---

# Verify:


Scan speed


↓

Backup speed


↓

Restore speed



---

# Registry Safety Stress Test


Create:


Large number of invalid registry entries.



Verify:


Detection accuracy.



---

# Security Testing


## Purpose


Ensure LeslieCleaner cannot damage system.



---

# File Safety Test


Test:


Protected   files.



Expected:


Cannot delete.



---

# Registry Safety Test


Test:


Critical registry keys.



Expected:


Backup required.



---

# Permission Test


Run:


Normal User


↓

Administrator



Verify:


Correct permission handling.



---

# Locked File Test


Open file by another process.



Expected:


Skip safely.



---

# System Protection Test


Test:


```

 

System32

Drivers

Boot


```



Expected:


Protected.



---

# Uninstall Safety Test


Verify:


Before uninstall:


Create restore point or backup.



---

# Startup Safety Test


Critical:


Microsoft startup protection.



---

# Test:


Disable operation on:


  Defender


  Update


System Services



Expected:


Blocked or protected.



---

# UI Automation Testing


## Purpose


Verify user experience.



---

# Testing Tool


Recommended:


FlaUI


or


WinAppDriver



---

# UI Test Cases


## Navigation Test


Verify:


All pages open correctly.



---

## Button Test


Verify:


Every button works.



---

## Dialog Test


Verify:


Confirmation dialogs appear.



---

## Progress Test


Verify:


Long operations show progress.



---

# UI Layout Test


Check:


Resolution:


```
1280×720

1920×1080

2560×1440


```



---

# Scaling Test


Verify:


  DPI:


100%

125%

150%



---

# Theme Testing


Verify:


Premium white theme consistency.



---

# Localization Testing


If multiple languages:


Check:


Text length


↓

Layout


↓

Encoding



---

# Compatibility Testing


---

# Hardware Compatibility


Test:


Low-end PC


↓

Gaming PC


↓

Laptop



---

# Hardware Conditions


Test:


Low disk space


↓

Slow HDD


↓

Fast SSD


↓

High CPU load



---

# Network Dependency Test


LeslieCleaner core functions must work offline.



---

# Update System Test


Verify:


Update check


↓

Download


↓

Verification


↓

Installation


↓

Rollback



---

# Plugin System Test


Test:


Install plugin


↓

Enable plugin


↓

Disable plugin


↓

Remove plugin



---

# Crash Recovery Testing


Force:


Application crash


Power interruption


Database interruption



---

# Verify:


Recovery


↓

Data integrity


↓

No corruption



---

# Final Two-Round Testing Requirement


After complete development:


Must perform two complete testing cycles.



---

# Round One


Purpose:


Functional correctness.



Process:


```

Execute all features


↓

Record bugs


↓

Fix bugs


↓

Retest


```



---

# Round Two


Purpose:


Optimization and release preparation.



Process:


```

Performance testing


↓

Memory testing


↓

Security testing


↓

Code cleanup


↓

Final verification


```



---

# Project Cleanup After Testing


After two rounds:


Remove:


```

Unused Code


Unused Dependencies


Temporary Files


Debug Logs


Old Build Files


Unused Resources


```



---

# Keep:


```

Source Code


↓

Required Libraries


↓

Database Migration


↓

Configuration


↓

Release Resources


```



---

# Release Acceptance Checklist


Before release:


## Functionality


□ Cleaner works


□ Registry cleaner works


□ Uninstaller works


□ Startup manager works


□ Browser cleaner works


□ Shredder works



---

## Safety


□ Backup works


□ Restore works


□ Protected paths work


□ Permission handling works



---

## Performance


□ Startup <3 seconds


□ Memory acceptable


□ No memory leak


□ Scan speed acceptable



---

## UI


□ Modern appearance


□ No layout issue


□ Smooth interaction



---

## Code Quality


□ No compiler warning


□ No unused files


□ Documentation complete


□ Version information complete



---

# Commercial Release Standard


LeslieCleaner can only be released when:


All critical tests pass.


No:


Critical bug


↓

Data loss issue


↓

Security vulnerability


↓

Performance failure



---

# Final QA Statement


Testing is not only finding bugs.

Testing ensures LeslieCleaner becomes a safe, stable, professional   optimization product.

END OF DOCUMENT

---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

