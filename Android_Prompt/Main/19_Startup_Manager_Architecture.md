# LeslieCleaner Android Development Bible - 19_Startup_Manager_Architecture

> Android Prompt ID: AP-MAIN-19-STARTUP-MANAGER-ARCHITECTURE-MD  
> Source Prompt: `Main\19_Startup_Manager_Architecture.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: Android lifecycle, JobScheduler/WorkManager, AppOps and platform capability boundaries; no unsupported desktop assumptions  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **Android lifecycle, JobScheduler/WorkManager, AppOps and platform capability boundaries; no unsupported desktop assumptions**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-MAIN-19-STARTUP-MANAGER-ARCHITECTURE-MD` in the Android traceability matrix.
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

Startup Manager Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner System startup management module.


The startup manager is responsible for:


Startup item detection


↓

Startup classification


↓

Boot impact analysis


↓

Safe optimization


↓

Startup restoration



---

# Core Design Philosophy


Startup optimization is not simply disabling everything.


The system must distinguish:


```

System Required Components


↓

User Installed Applications


```



---

# Main Goals


The startup manager must provide:


Accurate detection


↓

Clear classification


↓

Safe control


↓

Performance improvement


↓

Complete recovery



---

# Architecture Overview


```

Startup Manager


        |

        |

+----------------+

Startup Scanner

+----------------+

        |

        |

+----------------+

Classifier

+----------------+

        |

        |

+----------------+

Impact Analyzer

+----------------+

        |

        |

+----------------+

Action Controller

+----------------+

        |

        |

+----------------+

Restore Manager

+----------------+



```



---

# Module Structure


```

Startup


|

├── Scanner


├── Providers


├── Classifier


├── Analyzer


├── Controller


├── Backup


├── Restore


├── Report


```



---

# Startup Scanner


## Purpose


Collect all System startup entries.



---

# Startup Sources


The scanner must analyze:



## Registry Startup


Locations:



### Current User


```


```



---

### Local Machine


```


```



---

## Startup Folder


Locations:


User startup folder


↓

Common startup folder



---

## Task Scheduler


Analyze:


```

Scheduled Tasks


Task Actions


Triggers


```



---

## System Services


Analyze:


```

Service Startup Type


Executable Path


Publisher


```



---

# Startup Item Model


```csharp

public class StartupItem

{

    public long Id { get; set; }


    public string Name { get; set; }


    public string Publisher { get; set; }


    public string Path { get; set; }


    public string Command { get; set; }


    public StartupSource Source { get; set; }


    public StartupCategory Category { get; set; }


    public bool Enabled { get; set; }


}


```



---

# Startup Source Enumeration


```

Registry


StartupFolder


TaskScheduler


Service


```



---

# Scanner Interface


```csharp

public interface IStartupScanner

{

    Task<IEnumerable<StartupItem>>

    ScanAsync(CancellationToken token);


}

```



---

# Startup Classification System


## Purpose


Determine whether startup item is safe to optimize.



---

# Classification Categories


Required:



```

MicrosoftSystem


↓

UserApplication


↓

Unknown


```



---

# Microsoft System Category


Definition:


System built-in components.



Examples:


```

System Security


System Update


System Services


Hardware Drivers


```



---

# User Application Category


Definition:


Third-party software installed by user.



Examples:


```

Steam


Discord


Adobe


Cloud Sync Software


```



---

# Unknown Category


Definition:


Cannot confidently identify.



Examples:


```

Unknown Publisher


Missing File


Unverified Program


```



---

# Classification Rules


Analyze:


```

Publisher


↓

Digital Signature


↓

File Location


↓

Install Information


↓

Database Fingerprint


```



---

# Microsoft Detection Rules


Mark as Microsoft System if:


```

Publisher = Microsoft


OR


Path belongs to System folder


OR


Digital Signature verified by Microsoft


```



---

# Important Safety Rule


Publisher alone is not enough.



Must combine:



```

Publisher


+

Path


+

Signature


```



---

# User Application Detection


Mark as User Application if:


```

Installed under Program Files


OR


Registered in uninstall database


OR


Known software fingerprint


```



---

# Unknown Handling


Unknown items:


Default:


Do not recommend disabling automatically.



---

# Startup Provider Architecture


Each startup source should implement:



```csharp

public interface IStartupProvider

{

    Task<IEnumerable<StartupItem>>

    GetItemsAsync();


    Task EnableAsync(StartupItem item);


    Task DisableAsync(StartupItem item);


}

```



---

# Registry Startup Provider


Responsible:


Read and modify registry startup entries.



---

# Registry Provider Safety


Before modification:


Create backup.



---

# Startup Folder Provider


Responsible:


Manage shortcuts.



---

# Shortcut Analysis


Check:


```

Target Path


↓

Existence


↓

Publisher


```



---

# Task Scheduler Provider


Analyze:


```

Task Name


Action


Trigger


Status


```



---

# Service Provider


Analyze:


```

Service Name


Display Name


Startup Type


Executable


```



---

# Service Restrictions


Never disable:


System critical services.



---

# Startup Manager Database


Uses:


StartupItems table.



Stores:


```

Current Status


↓

Original Status


↓

Backup Information


↓

User Actions


```
---

# Startup Impact Analyzer


## Purpose


Evaluate how much each startup item affects System boot performance.



---

# Impact Factors


The analyzer calculates impact based on:



```

Startup Delay


↓

CPU Usage


↓

Memory Usage


↓

Disk Usage


↓

Application Importance


↓

Startup Frequency


```



---

# Startup Impact Score


Range:


```

0 - 100


```



---

# Score Meaning



## 0-30


Low Impact



Example:


Small helper applications.



---

## 31-70


Medium Impact



Example:


Cloud synchronization tools.



---

## 71-100


High Impact



Example:


Heavy background applications.



---

# Calculation Example


Formula:


```

Impact Score =


CPU Weight

+

Memory Weight

+

Disk Weight

+

Startup Time Weight


```



---

# Startup Time Measurement


Measure:


```

Application Launch Time


↓

First Response Time


↓

Background Initialization


```



---

# Performance Data Collection


Sources:


System Performance Counters


↓

Application History


↓

User Operation Data



---

# Startup Recommendation Engine


Purpose:


Generate optimization suggestions.



---

# Recommendation Rules



## High Impact + User Application


Recommend:


```

Disable


```



---

## Medium Impact + Frequently Used


Recommend:


```

Keep


or


Delay Start


```



---

## Microsoft System


Recommend:


```

Protected


```



---

# Disable Operation Design


## Purpose


Safely disable startup items.



---

# Disable Flow


```

User Selects Item


↓

Check Category


↓

Check Protection


↓

Create Backup


↓

Disable Startup


↓

Record Operation


```



---

# Disable Restrictions



Cannot disable automatically:



```

MicrosoftSystem


+

Protected


```



---

# Warning Message Example


```

This startup item belongs to System system components.

Disabling it may affect system stability.


```



---

# Backup System


Before disabling:


Save:


```

Original Location


↓

Original Value


↓

Original Status


```



---

# Registry Startup Backup Example


Store:


```

Registry Path:

HKCU\...\Run


Value Name:

ApplicationName


Value Data:

xxx.exe


```



---

# Restore System


Purpose:


Recover disabled startup items.



---

# Restore Flow


```

Select Item


↓

Read Backup


↓

Restore Original State


↓

Verify


↓

Update Database


```



---

# Delayed Startup System


## Purpose


Improve boot speed without disabling software.



---

# Delay Strategy


Instead of:


Start immediately.



Use:


```

System Login


↓

Wait Delay Time


↓

Start Application


```



---

# Delay Options


Example:


```

30 Seconds


60 Seconds


120 Seconds


300 Seconds


```



---

# Delay Implementation


Recommended:


System Task Scheduler.



---

# Delay Task Structure


```

Trigger:

User Login


↓

Delay:

60 seconds


↓

Action:

Launch Application


```



---

# Delay Restrictions


Do not delay:


```

Security Software


↓

Hardware Drivers


↓

System Components


```



---

# Startup Optimization Report


After optimization:


Generate:



```

Before Score


↓

After Score


↓

Disabled Items


↓

Delayed Items


↓

Estimated Improvement


```



---

# Example:


```

Startup Time:


45s


↓

28s



Improvement:


37%


```



---

# Startup History


Record:


```

Boot Time


↓

Startup Changes


↓

Performance Trend


```



---

# Boot Performance Tracking


Optional feature.



Monitor:


```

System Boot Duration


↓

Application Startup Time


```



---

# Startup Cache


Purpose:


Reduce repeated scanning.



---

# Cache Data


Store:


```

Startup Item


↓

Last Analysis


↓

Impact Score


↓

Publisher


```



---

# Cache Expiration


Recommended:


7 days.



---

# Multi-thread Scanning


Startup sources can scan independently.



---

# Example:


```

Registry Scanner


        |


Task Scheduler Scanner


        |


Service Scanner


        |


        ↓


Result Merge


```



---

# Exception Handling


Handle:


```

Access Denied


↓

Missing File


↓

Invalid Registry


↓

Service Failure


```



---

# Logging


Record:



```

Startup Scan


↓

Enable Operation


↓

Disable Operation


↓

Restore Operation


```



---

# Security Rules


Startup Manager must never:


Modify bootloader


↓

Disable System Security


↓

Remove system services


↓

Change critical drivers



---

# Testing Requirements



## Registry Startup Test


Test:


Enable


↓

Disable


↓

Restore



---

## Startup Folder Test


Test:


Shortcut creation


↓

Removal



---

## Task Scheduler Test


Test:


Create


↓

Disable


↓

Restore



---

## Service Test


Test:


Protected service handling.



---

# Performance Target


Startup scan:


<5 seconds



---

# Memory Target


Additional memory:


<50MB



---

# Startup Manager Final Requirement


The startup manager must achieve:


Faster boot time


↓

Clear recommendations


↓

Safe optimization


↓

Complete recovery


↓

No system damage



END OF DOCUMENT


---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

