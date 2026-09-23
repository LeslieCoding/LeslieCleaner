# LeslieCleaner Android Development Bible - 20_Privacy_Cleaner_Architecture

> Android Prompt ID: AP-MAIN-20-PRIVACY-CLEANER-ARCHITECTURE-MD  
> Source Prompt: `Main\20_Privacy_Cleaner_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-20-PRIVACY-CLEANER-ARCHITECTURE-MD` in the Android traceability matrix.
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

Privacy Cleaner Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner privacy cleaning module.


The privacy cleaner is responsible for:


Browser data analysis


↓

Privacy risk detection


↓

Temporary privacy data cleanup


↓

User-controlled removal


↓

Secure operation



---

# Core Design Philosophy


Privacy cleaning must respect user data.



The module must:

Protect personal information


↓

Never collect private content


↓

Never upload user data


↓

Always require user control



---

# Main Goals


The privacy module must provide:


Safe browser cleanup


↓

Transparent data handling


↓

Clear privacy explanation


↓

Reliable recovery



---

# Architecture Overview


```

Privacy Cleaner


        |

        |

+----------------+

Browser Scanner

+----------------+

        |

        |

+----------------+

Privacy Analyzer

+----------------+

        |

        |

+----------------+

Risk Engine

+----------------+

        |

        |

+----------------+

Cleanup Executor

+----------------+

        |

        |

+----------------+

Backup Manager

+----------------+


```



---

# Module Structure


```

Privacy


|

├── Browser


├── Scanner


├── Analyzer


├── Rules


├── Backup


├── Executor


├── Report


```



---

# Supported Browsers


Initial support:


```

Google Chrome


Microsoft Edge


Mozilla Firefox


Brave Browser


```



---

# Browser Detection System


Purpose:


Automatically find installed browsers.



---

# Detection Sources


Analyze:


```

Installation Directory


↓

Registry


↓

User Profile Directory


↓

Executable Location


```



---

# Browser Information Model


```csharp

public class BrowserInfo

{

    public long Id { get; set; }


    public string Name { get; set; }


    public string Version { get; set; }


    public string InstallPath { get; set; }


    public string ProfilePath { get; set; }


}


```



---

# Browser Provider Interface


```csharp

public interface IBrowserProvider

{

    Task<BrowserInfo>

    DetectAsync();


    Task<IEnumerable<PrivacyItem>>

    ScanAsync();


}

```



---

# Browser Profile Detection


Browsers may contain multiple profiles.



---

# Example:


Chrome:


```

Default


Profile 1


Profile 2


```



---

# Profile Model


Contains:


```

Profile Name


↓

Profile Path


↓

Last Used Time


```



---

# Privacy Item Model


```csharp

public class PrivacyItem

{

    public long Id { get; set; }


    public string Browser { get; set; }


    public string Type { get; set; }


    public string Path { get; set; }


    public long Size { get; set; }


    public int RiskLevel { get; set; }


}


```



---

# Privacy Data Categories


Main categories:



## Cache


Contains:


```

Images


↓

Scripts


↓

Temporary Resources


```



---

## History


Contains:


```

Visited URLs


↓

Browsing Records


```



---

## Cookies


Contains:


```

Website Session Data


↓

Login State


```



---

## Download History


Contains:


```

Downloaded File Records


```



---

## Session Data


Contains:


```

Open Tabs


↓

Recovery Data


```



---

# Privacy Risk Classification


Each item receives risk level.



---

# Low Risk


Example:


```

Browser Cache


Temporary Resources


```



---

# Medium Risk


Example:


```

Download History


Website History


```



---

# High Risk


Example:


```

Cookies


Saved Sessions


Authentication Data


```



---

# Critical Protection


Never clean automatically:


```

Saved Passwords


↓

Payment Information


↓

Security Certificates


```



---

# Cookie Management Strategy


Cookies require special handling.



---

# Default Behavior


Do not delete all cookies automatically.



---

# Reason:


Cookies may contain:


```

Login State


↓

User Preferences


↓

Authentication Information


```



---

# Cookie Options


Provide:


```

Keep All Cookies


↓

Remove Tracking Cookies


↓

Remove Selected Cookies


```



---

# Tracking Cookie Detection


Analyze:


```

Known Tracking Domains


↓

Third-party Cookies


↓

Expiration Information


```



---

# Browser Running Detection


Before cleanup:


Check:


```

Browser Process


↓

Locked Database


↓

Active Session


```



---

# Browser Close Handling


If browser running:


Show:


```

Chrome is running.

Close browser before cleaning.


```



---

# Privacy Scanner Flow


```

Detect Browser


↓

Find Profiles


↓

Analyze Data


↓

Calculate Risk


↓

Show Result


↓

User Selects


↓

Cleanup


```
---

# Browser Database Handling


## Purpose


Safely process browser internal databases.



---

# Browser Data Storage Types


Common formats:


```

SQLite Database


↓

JSON Configuration


↓

Binary Cache Files


```



---

# Database Examples


Chrome / Edge:


```

History


Cookies


Web Data


Login Data


```



---

# Firefox:


```

places.sqlite


cookies.sqlite


```



---

# Safety Requirement


Never directly modify browser databases while browser is running.



---

# Database Processing Flow


```

Detect Browser


↓

Check Process


↓

Create Temporary Backup


↓

Copy Database


↓

Analyze Copy


↓

Execute Cleanup


↓

Replace Original


```



---

# Temporary Database Copy


Reason:


Avoid corruption.



---

# Example:


Original:


```

Cookies


```



Processing:


```

Cookies Copy


↓

Analyze


↓

Modify


↓

Replace


```



---

# Browser Cache Cleanup


## Purpose


Remove unnecessary cached resources.



---

# Cache Detection


Analyze:


```

Cache Directory


↓

File Age


↓

File Size


↓

Last Access Time


```



---

# Cache Safety Rules


Allow cleanup:


```

Expired Cache


↓

Temporary Cache


```



---

# Keep:


```

Active Session Data


↓

Important Browser Configuration


```



---

# History Cleanup


## Purpose


Remove browsing history records.



---

# History Options


Provide:


```

Last Hour


↓

Today


↓

Last Week


↓

All Time


↓

Custom Range


```



---

# History Risk


Medium.



Reason:


Contains user browsing information.



---

# Download History Cleanup


## Important


Only remove records.



Do not delete downloaded files.



---

# Example:


Remove:


```

Download Record


```



Keep:


```

D:\Downloads\File.zip


```



---

# Session Cleanup


Session data includes:


```

Recently Closed Tabs


↓

Restore Information


```



---

# Default:


Protected.



---

# User Choice:


Allow manual cleanup.



---

# Cookie Selection Algorithm


## Purpose


Remove unwanted cookies safely.



---

# Cookie Classification


Categories:


```

Essential Cookie


↓

User Preference Cookie


↓

Tracking Cookie


↓

Advertising Cookie


```



---

# Essential Cookie


Examples:


```

Login Session


Security Token


```



Default:


Protected.



---

# Preference Cookie


Examples:


```

Language Setting


Theme Setting


```



Optional cleanup.



---

# Tracking Cookie


Examples:


```

Analytics


Advertising Tracking


```



Recommend cleanup.



---

# Cookie Risk Score


Formula:


```

Risk =


Tracking Level


+

Third Party Level


+

Expiration Status


```



---

# Privacy Risk Engine


## Purpose


Calculate privacy exposure.



---

# Risk Factors


Include:


```

Stored History Amount


↓

Cookie Count


↓

Tracking Domains


↓

Session Data


↓

Privacy Configuration


```



---

# Privacy Score


Range:


```

0-100


```



---

# Example:


Low Risk:


```

90


```



High Risk:


```

30


```



---

# Privacy Recommendation Engine


Generate suggestions.



---

# Example:


Problem:


```

3000 tracking cookies detected


```



Suggestion:


```

Remove tracking cookies


```



---

# Secure Cleanup Executor


Responsible:


Execute privacy cleanup.



---

# Cleanup Rules


Before removal:


Check:


```

User Selection


↓

Risk Level


↓

Backup Requirement


↓

Browser State


```



---

# Backup Strategy


For important privacy data:



Create:


```

BrowserDataBackup


```



---

# Backup Contents


Can include:


```

Cookies


↓

History


↓

Configuration


```



---

# Backup Restrictions


Never backup:


```

Passwords


↓

Payment Data


↓

Authentication Secrets


```



---

# Privacy Report


After cleanup:


Generate:


```

Browser Name


↓

Cleaned Items


↓

Released Space


↓

Privacy Improvement


```



---

# Example:


```

Chrome


Removed:


5200 Tracking Cookies


Freed:


850MB


```



---

# Multi-browser Parallel Processing


Supported.



---

# Execution Model


```

Chrome Scanner


        |


Edge Scanner


        |


Firefox Scanner


        |


        ↓


Result Merge


```



---

# Performance Optimization


## Cache Browser Path


Avoid repeated detection.



---

# Incremental Scan


Only analyze changed data.



---

# Large Cache Optimization


Use:


```

File Metadata


↓

Directory Statistics


↓

Batch Processing


```



---

# Exception Handling


Handle:


```

Browser Locked


↓

Database Corruption


↓

Permission Denied


↓

Missing Profile


```



---

# Logging


Record:


```

Browser Detection


↓

Scan Result


↓

Cleanup Action


↓

Failure


```



---

# Security Restrictions


Privacy cleaner must never:


Read passwords


↓

Upload browsing data


↓

Collect personal information


↓

Modify security settings



---

# Testing Requirements



## Browser Test


Test:


Chrome


↓

Edge


↓

Firefox


↓

Brave



---

## Data Safety Test


Verify:


History removal


↓

Cookie selection


↓

Cache cleanup



---

## Running Browser Test


Verify:


Locked database handling.



---

## Recovery Test


Verify:


Backup restore.



---

# Privacy Cleaner Final Requirement


The privacy module must provide:


Strong privacy protection


↓

Safe cleanup


↓

User control


↓

No personal data leakage


↓

Reliable recovery



END OF DOCUMENT


---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

