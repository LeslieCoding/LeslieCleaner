# LeslieCleaner Android Development Bible - 15_Data_Model_And_Database_Design

> Android Prompt ID: AP-MAIN-15-DATA-MODEL-AND-DATABASE-DESIGN-MD  
> Source Prompt: `Main\15_Data_Model_And_Database_Design.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-15-DATA-MODEL-AND-DATABASE-DESIGN-MD` in the Android traceability matrix.
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

Data Model And Database Design Specification


Priority:

★★★★★


---

# Purpose


This document defines the complete database architecture of LeslieCleaner.


The goal:


Provide a stable, efficient, safe data storage system.


The database must support:


User Configuration

↓

Scan Records

↓

Cleanup History

↓

Application Information

↓

Startup Management

↓

Registry Backup

↓

System Analysis

↓

AI Recommendations



---

# Database Technology


Recommended:


SQLite


Version:


SQLite 3.x



---

# Database Design Philosophy


The database must be:


Lightweight


↓

Fast


↓

Reliable


↓

Easy Backup


↓

Easy Migration



---

# Database Location


Recommended:


Normal Installation:


```

C:\ProgramData\LeslieCleaner\Database


```



Portable Mode:


```

.\Data\Database


```



---

# Main Database File


Name:


```
LeslieCleaner.db


```



---

# Database Characteristics


SQLite advantages:


No server dependency


↓

Fast local access


↓

Easy backup


↓

Suitable for desktop applications



---

# Database Access Architecture


Database access must follow:


```

ViewModel


↓

Service


↓

Repository


↓

SQLite


```



---

# Forbidden


Direct database operation:


UI


↓

SQLite



---

# ORM Recommendation


Recommended:


Entity Framework Core SQLite


or


Dapper



---

# Preferred


Dapper


Reason:


Lightweight


↓

High performance


↓

Suitable for desktop software



---

# Database Naming Rules


Table:


PascalCase


Example:


```
StartupItems


```



---

# Column:


PascalCase


Example:


```
CreatedTime


```



---

# Primary Key


Standard:


```
Id


```



---

# Time Fields


Use:


```
CreatedTime


UpdatedTime


```



Format:


UTC Time



---

# Database Table Overview


Main tables:


```

UserSettings


↓

ScanRecords


↓

ScanItems


↓

CleanupRecords


↓

Applications


↓

StartupItems


↓

RegistryBackups


↓

OperationLogs


↓

AIRecommendations


```



---

# Table 1

# UserSettings


Purpose:


Store user preferences.



---

# Structure


```

UserSettings


Id


Key


Value


ValueType


Description


CreatedTime


UpdatedTime


```



---

# Field Explanation


## Id


Primary key.



---

## Key


Configuration name.



Example:


```
Theme


```



---

## Value


Configuration value.



Example:


```
Light


```



---

## ValueType


Data type.



Example:


```
String


Boolean


Integer


```



---

## Description


Configuration explanation.



---

# Example Data


```

Theme

Light


ScanThreads

8


AutoBackup

true


```



---

# Table 2

# ScanRecords


Purpose:


Record every scan operation.



---

# Structure


```

ScanRecords


Id


ScanType


StartTime


EndTime


Duration


TotalItems


TotalSize


Status


CreatedTime


```



---

# Field Explanation


## ScanType


Example:


```
QuickScan

DeepScan

CustomScan


```



---

## Status


Example:


```
Completed


Cancelled


Failed


```



---

## TotalSize


Unit:


Bytes



---

# Usage


Display:


Scan history


↓

Performance analysis


↓

Statistics



---

# Table 3

# ScanItems


Purpose:


Store detailed scan results.



---

# Structure


```

ScanItems


Id


ScanRecordId


ItemType


Name


Path


Size


RiskLevel


Selected


Status


CreatedTime


```



---

# Field Explanation


## ScanRecordId


Relationship:


```

ScanRecords.Id


```



---

## ItemType


Examples:


```
TempFile


Cache


Registry


Residual


```



---

## RiskLevel


Values:


```

Low


Medium


High


Critical


```



---

## Selected


User selection state.



Example:


```

true


false


```



---

## Status


Example:


```

Detected


Cleaned


Skipped


Failed


```



---

# Table 4

# CleanupRecords


Purpose:


Store cleanup execution history.



---

# Structure


```

CleanupRecords


Id


ScanRecordId


CleanType


ItemCount


CleanSize


StartTime


EndTime


Status


CreatedTime


```



---

# Field Explanation


## CleanType


Examples:


```

JunkClean


RegistryRepair


ResidualClean


```



---

## CleanSize


Amount of released storage.



Unit:


Bytes



---

# Cleanup History Usage


Display:


Dashboard statistics


↓

Recent activity


↓

User reports



---

# Table 5

# Applications


Purpose:


Store installed software information.



---

# Structure


```

Applications


Id


Name


Publisher


Version


InstallPath


InstallDate


Size


UninstallCommand


Fingerprint


RiskLevel


CreatedTime


UpdatedTime


```



---

# Field Explanation


## Name


Application name.



---

## Publisher


Software developer.



---

## InstallPath


Installation directory.



---

## UninstallCommand


Official uninstall command.



---

## Fingerprint


Used for residual detection.



Contains:


Files


↓

Registry paths


↓

Services


↓

Startup entries



---

# Table Usage


Used by:


Software Manager


↓

Uninstaller


↓

Residual Scanner



---
---

# Table 6

# StartupItems


## Purpose


Store System startup item information.


This table is critical for startup optimization.



---

# Design Requirement


Startup items must be strictly classified:


```

Microsoft System Startup


↓

User Installed Application Startup


```



---

# Structure


```

StartupItems


Id


Name


Publisher


Path


Command


StartupType


Category


ImpactLevel


Status


IsProtected


CanDisable


Enabled


CreatedTime


UpdatedTime


```



---

# Field Explanation



## Name


Startup item name.



Example:


```

System Defender


Chrome


OneDrive


```



---

## Publisher


Software publisher.



Example:


```

Microsoft Corporation


Google LLC


```



---

## Path


Executable path.



Example:


```

C:\Program Files\xxx\xxx.exe


```



---

## Command


Startup command.



---

## StartupType


Source type.



Values:


```

Registry


StartupFolder


TaskScheduler


Service


```



---

## Category


Critical field.



Values:



### MicrosoftSystem


Represents:


System built-in startup components.



Example:


```

System Security


System Update


System Services


```



---

### UserApplication


Represents:


User-installed software startup items.



Example:


```

Steam


Discord


Adobe


```



---

## ImpactLevel


Startup impact.



Values:


```

Low


Medium


High


Critical


```



---

## Status


Current state.



Values:


```

Enabled


Disabled


Delayed


```



---

## IsProtected


Whether protected.



Example:


```

true


```



Meaning:


Cannot be disabled normally.



---

## CanDisable


Whether user can disable.



Example:


```

false


```



---

## Enabled


Current startup status.



---

# Startup Safety Rules


Before disabling:



Check:


```

Category


↓

IsProtected


↓

CanDisable


↓

RiskLevel


```



---

# Important Logic


If:


```

Category = MicrosoftSystem


AND


IsProtected = true


```



Then:


Disable operation blocked.



---

# Table 7

# RegistryBackups


## Purpose


Store registry backup information.



---

# Structure


```

RegistryBackups


Id


BackupName


RegistryPath


BackupFile


BackupSize


OperationType


CreatedTime


Status


```



---

# Field Explanation



## BackupName


Example:


```

BeforeRegistryRepair_001


```



---

## RegistryPath


Modified registry location.



Example:


```

HKCU\Software\xxx


```



---

## BackupFile


Backup file path.



Example:


```

Backup\Registry\001.reg


```



---

## OperationType


Values:


```

Repair


Delete


Modify


Restore


```



---

## Status


Values:


```

Created


Used


Restored


Failed


```



---

# Registry Backup Rules


Before:


Any registry modification



Must:


Create backup record.



---

# Table 8

# OperationLogs


## Purpose


Record all important user operations.



---

# Structure


```

OperationLogs


Id


Module


Operation


Description


Level


Result


ErrorMessage


CreatedTime


```



---

# Field Explanation



## Module


Example:


```

Cleaner


Registry


Startup


Uninstaller


```



---

## Operation


Example:


```

Scan


Delete


Restore


DisableStartup


```



---

## Level


Values:


```

Info


Warning


Error


Critical


```



---

## Result


Values:


```

Success


Failed


Cancelled


```



---

# Logging Requirement


Every dangerous action requires logging.



Examples:


Registry modification


↓

File deletion


↓

Startup change


↓

Uninstall operation



---

# Table 9

# AIRecommendations


## Purpose


Store AI analysis results.



---

# Structure


```

AIRecommendations


Id


Category


Problem


Analysis


Suggestion


Impact


Status


CreatedTime


```



---

# Field Explanation



## Category


Examples:


```

Startup


Storage


Privacy


Performance


```



---

## Problem


Detected issue.



Example:


```

Chrome cache is large


```



---

## Analysis


AI explanation.



---

## Suggestion


Recommended action.



---

## Impact


Example:


```

Improve startup speed


Free storage space


```



---

## Status


Values:


```

Pending


Accepted


Rejected


Completed


```



---

# Database Index Design


Indexes improve query performance.



---

# Required Indexes



## Applications


Index:


```
Name


Publisher


```



Purpose:


Fast search.



---

## StartupItems


Index:


```
Category


Enabled


```



Purpose:


Startup filtering.



---

## ScanItems


Index:


```
ScanRecordId


ItemType


```



Purpose:


Fast result loading.



---

## OperationLogs


Index:


```
Module


CreatedTime


```



---

# Database Optimization Rules


## Use Pagination


Large lists must not load all data.



Example:


Applications:


1000+ records



---

# Query Limit


Use:


```
LIMIT


OFFSET


```



---

# Transaction Usage


Important operations require transactions.



Examples:


Cleanup


↓

Registry Repair


↓

Database Migration



---

# Transaction Example Flow


```

Begin Transaction


↓

Backup


↓

Modify


↓

Verify


↓

Commit


```



Failure:


Rollback.



---

# Database Backup Strategy


Backup:


```

LeslieCleaner.db


```



---

# Backup Timing


Recommended:


Before:


Major upgrade


↓

Database migration


↓

Large cleanup operation



---

# Database Integrity Check


Periodically verify:



```

Table Integrity


↓

Index Integrity


↓

Data Consistency


```



---

# Database Recovery


If corrupted:


Process:


```

Close Application


↓

Restore Backup


↓

Verify


↓

Restart


```



---

# Data Security Rules


Sensitive data:



Must not store:


Passwords


↓

Browser cookies


↓

Private content



---

# Privacy Requirement


Database logs should store:


Operation information only.



---

# Database Performance Target


Normal query:


<50ms



Large query:


<500ms



---

# Database Final Requirement


LeslieCleaner database must provide:


Reliable storage


↓

Fast querying


↓

Safe recovery


↓

Easy migration


↓

Long-term stability



END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

