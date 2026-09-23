# LeslieCleaner Android Development Bible - 22_Backup_And_Restore_System

> Android Prompt ID: AP-MAIN-22-BACKUP-AND-RESTORE-SYSTEM-MD  
> Source Prompt: `Main\22_Backup_And_Restore_System.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-22-BACKUP-AND-RESTORE-SYSTEM-MD` in the Android traceability matrix.
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

Backup And Restore System Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the complete backup and restore architecture of LeslieCleaner.


The backup system provides protection for:


Registry operations


↓

Cleanup operations


↓

Uninstall operations


↓

Configuration changes


↓

System optimization changes



---

# Core Design Philosophy


Every risky operation must be reversible.



The system follows:


```

Analyze


↓

Backup


↓

Execute


↓

Verify


↓

Restore If Needed


```



---

# Main Goals


The backup system must provide:


Safety


↓

Reliability


↓

Fast recovery


↓

Low storage usage


↓

Clear management



---

# Architecture Overview


```

Backup System


        |

        |

+----------------+

Backup Manager

+----------------+

        |

        |

+----------------+

Backup Providers

+----------------+

        |

        |

+----------------+

Storage Engine

+----------------+

        |

        |

+----------------+

Restore Engine

+----------------+

        |

        |

+----------------+

Verification

+----------------+


```



---

# Module Structure


```

Backup


|

├── Manager


├── Providers


├── Storage


├── Compression


├── Verification


├── Restore


├── Cleanup


├── Report


```



---

# Backup Types


The system supports:



## Registry Backup


Purpose:


Protect registry modifications.



---

## File Backup


Purpose:


Protect important files before operations.



---

## Configuration Backup


Purpose:


Save application settings.



---

## Database Backup


Purpose:


Protect LeslieCleaner internal data.



---

# Backup Manager


## Responsibility


Coordinate all backup operations.



---

# Backup Manager Interface


```csharp

public interface IBackupManager

{

    Task<BackupResult>

    CreateBackupAsync(

        BackupRequest request);


    Task<RestoreResult>

    RestoreAsync(

        BackupInfo backup);


}

```



---

# Backup Request Model


```csharp

public class BackupRequest

{

    public string Type { get; set; }


    public IEnumerable<string> Items { get; set; }


    public string Description { get; set; }


}

```



---

# Backup Information Model


```

BackupInfo


Id


Name


Type


Size


Path


CreatedTime


Status


```



---

# Backup Storage Architecture


Recommended structure:


```

LeslieCleaner


|

└── Backup


    |

    ├── Registry


    |

    ├── Files


    |

    ├── Database


    |

    └── Config


```



---

# Backup Naming Convention


Format:


```

Backup_Type_Date_Time


```



Example:


```

Registry_20260726_120000


```



---

# Registry Backup Provider


## Purpose


Export registry changes.



---

# Registry Backup Process


```

Select Keys


↓

Export .reg File


↓

Calculate Hash


↓

Save Metadata


↓

Complete


```



---

# Registry Backup Data


Store:


```

Registry Path


↓

Original Value


↓

Backup File


↓

Creation Time


```



---

# File Backup Provider


## Purpose


Protect files before deletion.



---

# File Backup Strategy


Three levels:



---

# Level 1

# Metadata Backup


Store:


```

Path


↓

Size


↓

Hash


↓

Modified Time


```



---

# Level 2

# Move Backup


Move files into backup storage.



---

# Level 3

# Full Copy Backup


Create complete duplicate.



---

# Backup Level Selection


Based on:


```

Risk Level


↓

File Size


↓

Operation Type


```



---

# Example:


Temporary Cache:


```

Metadata Only


```



Registry Modification:


```

Full Backup


```



---

# Configuration Backup


Purpose:


Save LeslieCleaner settings.



---

# Configuration Data


Include:


```

User Preferences


↓

Scan Rules


↓

Whitelist


↓

Blacklist


↓

Custom Settings


```



---

# Configuration Format


Recommended:


```

JSON


```



Example:


```

settings.json


```



---

# Database Backup


Purpose:


Protect application database.



---

# Database Backup Content


Include:


```

LeslieCleaner.db


↓

Migration Information


↓

User Data


```



---

# SQLite Backup Method


Use:


SQLite Backup API



---

# Database Backup Frequency


Recommended:



Automatic:


Daily



Manual:


Before major operations



---

# Backup Metadata Database


Store:


```

BackupRecords


```



Structure:


```

Id


BackupType


BackupPath


Size


Hash


Status


CreatedTime


```



---

# Backup Verification


Every backup must be verified.



---

# Verification Methods


Check:


```

File Exists


↓

Size Match


↓

Hash Match


↓

Readable


```



---

# Hash Verification


Recommended:


SHA-256



---

# Backup Compression


Purpose:


Reduce storage usage.



---

# Compression Format


Recommended:


```

ZIP


```



---

# Compression Rules


Compress:


```

Large Files


↓

Database Backup


↓

Configuration Backup


```



---

# Do Not Compress:


Already compressed:


```

ZIP


RAR


MP4


ISO


```



---

# Backup Security


Backup files must:


Require local access


↓

Prevent unauthorized modification


↓

Maintain integrity



---

---

# Restore Engine


## Purpose


Restore system state after failed or unwanted operations.



---

# Restore Types



## Registry Restore


Restore:


```

Registry Keys


↓

Registry Values


```



---

## File Restore


Restore:


```

Deleted Files


↓

Original Location


```



---

## Configuration Restore


Restore:


```

Application Settings


↓

User Preferences


```



---

## Database Restore


Restore:


```

LeslieCleaner Database


```



---

# Restore Flow


```

User Selects Backup


↓

Verify Backup Integrity


↓

Create Current State Backup


↓

Execute Restore


↓

Validate Result


↓

Generate Report


```



---

# Restore Safety Principle


Never overwrite current data directly.



---

# Correct Process:


```

Current State Backup


↓

Restore Previous State


↓

Verify


```



---

# Restore Conflict Handling


If target exists:


Options:


```

Replace


↓

Keep Current


↓

Create Copy


```



---

# Restore Verification


After restore:


Check:


```

File Exists


↓

Hash Match


↓

Registry Valid


↓

Database Accessible


```



---

# Restore Failure Handling


If restore fails:


```

Stop Operation


↓

Rollback


↓

Generate Error Report


```



---

# Automatic Backup Strategy


## Purpose


Protect user without requiring manual operation.



---

# Automatic Backup Triggers



## Before Risky Operations


Examples:


```

Registry Repair


↓

Forced Uninstall


↓

Large Cleanup


```



---

## Before Software Upgrade


Backup:


```

Configuration


↓

Database


```



---

## Scheduled Backup


Optional.



---

# Scheduled Backup Options


User can configure:


```

Daily


↓

Weekly


↓

Monthly


```



---

# Default Automatic Backup


Recommended:


Enable only important backups.



---

# Avoid:


Creating huge backups every day.



---

# Backup Retention Management


## Purpose


Control storage usage.



---

# Retention Policy


Default:


```

Keep Latest 10 Backups


```



---

# User Configuration


Allow:


```

Number Based


↓

Time Based


↓

Storage Size Based


```



---

# Storage Size Rule


Example:


Maximum:


```

10GB


```



When exceeded:


Remove oldest backups.



---

# Backup Cleanup Process


```

Calculate Backup Size


↓

Sort By Time


↓

Delete Old Backups


↓

Verify Space


```



---

# Backup Priority


Never delete:


Recent backup


↓

Important operation backup


↓

User locked backup



---

# Backup Lock


User can protect backup.



---

# Locked Backup


Cannot be automatically deleted.



---

# Backup Compression Optimization


For large backups:



Use:


```

Streaming Compression


```



---

# Avoid:


Loading entire backup into memory.



---

# Backup Encryption


Optional feature.



---

# Encryption Usage


Suitable for:


```

Sensitive Configuration


↓

Private Backup Data


```



---

# Encryption Rules


Never store:


Plaintext sensitive backup data.



---

# Backup Integrity


Each backup stores:


```

Hash


↓

Size


↓

Creation Time


↓

Version


```



---

# Disaster Recovery System


## Purpose


Recover LeslieCleaner after serious failure.



---

# Disaster Scenarios


Support:


```

Database Corruption


↓

Configuration Damage


↓

Failed Upgrade


↓

Incomplete Cleanup


```



---

# Recovery Mode


Application startup checks:



```

Database Valid


↓

Configuration Valid


↓

Required Files Exist


```



---

# Recovery Detection


If failure:


Enter:


```

Recovery Mode


```



---

# Recovery Mode Functions


Provide:


```

Restore Backup


↓

Repair Installation


↓

Reset Configuration


↓

Export Logs


```



---

# Emergency Backup


Before recovery:


Create:


```

Emergency Snapshot


```



---

# Recovery Report


Contains:


```

Problem


↓

Recovery Action


↓

Result


↓

Remaining Issues


```



---

# Backup Database Tables


## BackupRecords


Structure:


```

Id


Type


Name


Path


Size


Hash


Status


CreatedTime


```



---

## RestoreRecords


Structure:


```

Id


BackupId


Operation


Result


Error


CreatedTime


```



---

# Performance Optimization


## Backup Streaming


Large files use:


```

Stream Copy


```



---

# Parallel Backup


Allowed for independent files.



---

# Thread Control


Avoid excessive disk usage.



---

# Recommended:


```

2-4 Workers


```



---

# Logging Requirements


Record:


```

Backup Created


↓

Backup Verified


↓

Restore Started


↓

Restore Completed


↓

Restore Failed


```



---

# Security Requirements


Backup system must never:


Upload backup data


↓

Access unrelated user files


↓

Delete protected backups


↓

Modify backups silently



---

# Testing Requirements



## Backup Test


Verify:


Creation


↓

Storage


↓

Verification



---

## Restore Test


Verify:


Complete recovery.



---

## Failure Test


Simulate:


Disk error


↓

Permission error


↓

Corrupted backup



---

## Retention Test


Verify:


Old backup cleanup.



---

# Backup System Final Requirement


The backup and restore system must provide:


Reliable protection


↓

Fast recovery


↓

Low storage overhead


↓

Complete transparency


↓

Long-term stability



END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

