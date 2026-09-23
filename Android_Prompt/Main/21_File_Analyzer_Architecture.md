# LeslieCleaner Android Development Bible - 21_File_Analyzer_Architecture

> Android Prompt ID: AP-MAIN-21-FILE-ANALYZER-ARCHITECTURE-MD  
> Source Prompt: `Main\21_File_Analyzer_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-21-FILE-ANALYZER-ARCHITECTURE-MD` in the Android traceability matrix.
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

File Analyzer Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and design standards of the LeslieCleaner file analysis module.


The file analyzer is responsible for:


Disk analysis


↓

Large file detection


↓

Duplicate file detection


↓

Storage visualization


↓

Cleanup recommendation



---

# Core Design Philosophy


File analysis is not a simple file listing tool.


The system must understand:


Where space is used


↓

Why space is occupied


↓

Whether files are safe to remove



---

# Main Goals


The file analyzer must provide:


Fast scanning


↓

Accurate classification


↓

Low memory usage


↓

Safe recommendations


↓

Clear visualization



---

# Architecture Overview


```

File Analyzer


        |

        |

+----------------+

Directory Scanner

+----------------+

        |

        |

+----------------+

File Classifier

+----------------+

        |

        |

+----------------+

Hash Engine

+----------------+

        |

        |

+----------------+

Analysis Engine

+----------------+

        |

        |

+----------------+

Report Generator

+----------------+


```



---

# Module Structure


```

FileAnalyzer


|

├── Scanner


├── Classifier


├── Hash


├── DuplicateDetector


├── LargeFileDetector


├── Statistics


├── Report


```



---

# Directory Scanner


## Purpose


Collect file information from storage devices.



---

# Scanner Responsibilities


Analyze:


```

File Path


↓

File Size


↓

File Type


↓

Modified Time


↓

Created Time


↓

Access Time


```



---

# Scanner Interface


```csharp

public interface IFileScanner

{

    IAsyncEnumerable<FileInfo>

    ScanAsync(

        string path,

        CancellationToken token);


}

```



---

# File Information Model


```csharp

public class FileInfoModel

{

    public string Path { get; set; }


    public long Size { get; set; }


    public string Extension { get; set; }


    public DateTime ModifiedTime { get; set; }


    public FileCategory Category { get; set; }


}

```



---

# File Categories


The analyzer classifies files.



---

# System Files


Examples:


```

System


Drivers


System Components


```



---

# Application Files


Examples:


```

Installed Software


Game Files


Development Tools


```



---

# User Files


Examples:


```

Documents


Images


Videos


Projects


```



---

# Temporary Files


Examples:


```

Cache


Logs


Temporary Data


```



---

# Unknown Files


Files that cannot be confidently classified.



---

# Classification Rules


Use:


```

Path


↓

Extension


↓

Publisher


↓

Application Relationship


↓

File Metadata


```



---

# Protected Path System


The analyzer must protect:



```


System Reserved Areas


```



---

# Large File Detector


## Purpose


Find files consuming large storage.



---

# Default Threshold


Recommended:



```

>500MB


```



---

# User Configuration


Allow:


```

100MB


500MB


1GB


5GB


Custom


```



---

# Large File Information


Display:


```

File Name


↓

Path


↓

Size


↓

Last Modified


↓

Category


```



---

# Large File Risk Analysis


A large file is not automatically removable.



---

# Example:


Game Installation:


```

100GB


```



Recommendation:


Keep.



---

# Example:


Old ISO Backup:


```

20GB


```



Recommendation:


Review.



---

# Large File Recommendation Engine


Analyze:


```

File Age


↓

Usage Frequency


↓

File Type


↓

Location


```



---

# Recommendation Levels



## Safe Suggestion


Example:


Old temporary archive.



---

## Review Required


Example:


Large unknown file.



---

## Protected


Example:


System image file.



---

# Duplicate File Detector


## Purpose


Find identical files.



---

# Detection Strategy


Must avoid:


Full hashing all files immediately.



---

# Detection Pipeline


```

File Size Filtering


↓

Partial Hash


↓

Full Hash


↓

Duplicate Group


```



---

# Step 1

# Size Filtering


Only compare:


Files with same size.



---

# Step 2

# Partial Hash


Calculate:


File beginning


↓

File middle


↓

File ending



---

# Step 3

# Full Hash


For candidates:



Calculate complete hash.



---

# Supported Hash Algorithms


Recommended:


```

SHA-256


```



Optional:


```

MD5


```



---

# Hash Model


```

FileHash


Id


FilePath


FileSize


Algorithm


HashValue


CreatedTime


```



---

# Duplicate Group Model


```

DuplicateGroup


Id


Hash


TotalSize


FileCount


CreatedTime


```



---

# Duplicate Safety Rules


Never automatically delete duplicates.



---

# Reason:


Files may have:


Different purposes


↓

Different locations


↓

User importance



---

# Duplicate Recommendation


Show:


```

Possible duplicate


↓

Original file suggestion


↓

Space recoverable


```



---

# Duplicate Selection Logic


Prefer keeping:


```

Newest File


↓

User Folder


↓

Frequently Accessed File


```
---

# Disk Space Statistics System


## Purpose


Provide a clear overview of storage usage.



---

# Statistics Categories


Analyze:



```

System Usage


↓

Application Usage


↓

User Data


↓

Temporary Data


↓

Unknown Data


```



---

# Storage Analysis Model


```csharp

public class StorageStatistics

{

    public long TotalSize { get; set; }


    public long UsedSize { get; set; }


    public long FreeSize { get; set; }


    public long TemporarySize { get; set; }


    public long ApplicationSize { get; set; }


    public long UserDataSize { get; set; }

}

```



---

# Drive Scanner


Support:


```

C:


↓

D:


↓

External Drives


```



---

# Drive Information


Display:


```

Drive Name


↓

Capacity


↓

Used Space


↓

Free Space


↓

Health Status


```



---

# Storage Visualization


The UI should provide:



## Usage Ring Chart


Display:


```

Used


Free


Cleanable


```



---

## Category Ranking


Example:


```

Games

250GB


Applications

80GB


Documents

60GB


Cache

20GB


```



---

# File Tree Analysis


Purpose:


Analyze storage hierarchy.



---

# Tree Structure


```

Drive


|

├── Folder


│

├── SubFolder


│

└── File


```



---

# Tree Optimization


Avoid loading entire tree.



---

# Use:


Lazy Loading



---

# Example:


Only expand:


```

Users


```



when user opens it.



---

# File Search System


Purpose:


Quickly locate files.



---

# Search Conditions


Support:


```

File Name


↓

Extension


↓

Size Range


↓

Date Range


↓

Location


```



---

# Search Index


Optional:


Create lightweight index.



---

# Index Content


Store:


```

File Path


↓

Size


↓

Modified Time


↓

Category


```



---

# Index Update


Update:


When file changes.



---

# Real-time Monitoring


Optional.



Use:


System FileSystemWatcher.



---

# Hash Engine Optimization


Hash calculation is expensive.



---

# Optimization Strategy



## Small Files


Calculate:


Full SHA-256.



---

## Medium Files


Calculate:


Partial Hash first.



---

## Large Files


Use:


Multi-stage verification.



---

# Hash Pipeline


```

File Size


↓

Quick Hash


↓

Partial Hash


↓

Full Hash


```



---

# Parallel Hash Calculation


Allowed:



```

Multiple Worker Tasks


```



---

# Thread Control


Use:


```

SemaphoreSlim


```



---

# Memory Control


Never load:


Entire file into memory.



---

# Correct:


Stream reading.



---

# Example:


Wrong:


```

Read 50GB File


into Memory


```



Correct:


```

Read Blocks


↓

Calculate Hash


```



---

# Large File Scan Optimization


For large disks:



Use:


```

Directory Filtering


↓

Extension Filtering


↓

Size Filtering


```



---

# Ignore Paths


Default ignore:


```

System


↓

Program Files


↓

System Volume Information


```



---

# Exception Handling


Handle:


```

Access Denied


↓

File Locked


↓

Invalid Path


↓

Broken Link


```



---

# Cleanup Recommendation Engine


## Purpose


Recommend possible storage optimization.



---

# Recommendation Factors


Include:


```

File Age


↓

File Type


↓

Size


↓

Location


↓

Usage Frequency


```



---

# Recommendation Levels



## Safe


Example:


```

Temporary Files


Old Logs


```



---

## Review


Example:


```

Large Archive


Old Backup


```



---

## Protected


Example:


```

System Files


Application Files


```



---

# Automatic Cleanup Rules


File analyzer does not delete directly.



---

# Correct Flow:


```

Analyze


↓

Recommend


↓

User Confirm


↓

Cleanup Module


```



---

# Integration With Cleaner Engine


File Analyzer provides:


```

Candidate Files


↓

Risk Information


↓

Size Information


```



---

# Integration With Dashboard


Provide:


```

Storage Health


↓

Largest Files


↓

Recoverable Space


```



---

# Database Integration


Store:



## FileAnalysisRecords


Structure:


```

Id


Path


Size


Category


AnalysisTime


```



---

# Analysis Cache


Purpose:


Avoid repeated scans.



---

# Cache Data


Store:


```

File Path


↓

Size


↓

Hash


↓

Category


↓

Last Scan Time


```



---

# Cache Expiration


Recommended:


30 days.



---

# Performance Targets



## Normal Disk


Scan:


<30 seconds



---

## Large Disk


Scan:


<5 minutes



---

# Memory Target


Additional usage:


<200MB



---

# Security Restrictions


File Analyzer must never:


Delete automatically


↓

Modify system files


↓

Access protected personal data


↓

Upload file information



---

# Testing Requirements



## Small Disk Test


Verify:


Basic scanning.



---

## Large Disk Test


Millions of files.



---

## Duplicate Test


Same file different paths.



---

## Permission Test


Protected folders.



---

## Performance Test


Memory and CPU usage.



---

# File Analyzer Final Requirement


The file analyzer must provide:


Accurate storage understanding


↓

Useful cleanup suggestions


↓

Fast scanning


↓

Safe operation


↓

Professional user experience



END OF DOCUMENT


---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

