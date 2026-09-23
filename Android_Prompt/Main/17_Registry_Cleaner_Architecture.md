# LeslieCleaner Android Development Bible - 17_Registry_Cleaner_Architecture

> Android Prompt ID: AP-MAIN-17-REGISTRY-CLEANER-ARCHITECTURE-MD  
> Source Prompt: `Main\17_Registry_Cleaner_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-17-REGISTRY-CLEANER-ARCHITECTURE-MD` in the Android traceability matrix.
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

Registry Cleaner Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner Registry Cleaner module.


The registry cleaner is responsible for:


Registry analysis

↓

Invalid entry detection

↓

Risk evaluation

↓

Backup creation

↓

Safe repair

↓

Restore operation



---

# Core Design Philosophy


Registry cleaning is a high-risk operation.


Therefore:


Safety > Cleaning Amount



The system must never aggressively delete registry entries just to increase cleaning results.



---

# Main Goals


The registry module must provide:


Accurate detection


↓

Low false-positive rate


↓

Complete backup


↓

Reliable restore


↓

Transparent user control



---

# Architecture Overview


```

Registry Cleaner


        |

        |

+----------------+

Registry Scanner

+----------------+

        |

        |

+----------------+

Rule Analyzer

+----------------+

        |

        |

+----------------+

Risk Engine

+----------------+

        |

        |

+----------------+

Backup Manager

+----------------+

        |

        |

+----------------+

Repair Executor

+----------------+



```



---

# Module Structure


```

Registry


|

├── Scanner


├── Providers


├── Rules


├── Analyzer


├── Backup


├── Executor


├── Restore


├── Report


```



---

# Registry Provider Layer


Purpose:


Provide safe System Registry access.



---

# Provider Responsibilities


Handle:


Registry reading


↓

Registry enumeration


↓

Registry backup


↓

Registry modification



---

# Registry API Abstraction


Never directly call System Registry APIs everywhere.



---

# Required Interface



```csharp

public interface IRegistryProvider

{

    Task<IEnumerable<RegistryEntry>> ScanAsync();


    Task BackupAsync();


    Task RestoreAsync();

}

```



---

# Supported Registry Areas


Main:


```

HKEY_CURRENT_USER


HKEY_LOCAL_MACHINE


HKEY_CLASSES_ROOT


```



---

# Protected Registry Areas


Never automatically modify:


```

System Boot Configuration


System Drivers


Security Configuration


Hardware Information


```



---

# Registry Scanner


Purpose:


Find potentially invalid registry entries.



---

# Scanner Categories


Include:



## Invalid Application Paths


Detect:


Programs pointing to missing files.



Example:


```

C:\OldSoftware\App.exe


```



---

## Uninstalled Software Residuals


Detect:


Remaining keys after uninstall.



Example:


```

Old Application Name


Old Publisher


```



---

## Broken File Associations


Detect:


File extensions pointing to unavailable applications.



Example:


```

.txt


→

Missing Program


```



---

## Invalid Startup Registry


Detect:


Startup entries pointing to deleted programs.



---

## Empty Registry Entries


Detect:


Unused empty keys.



---

# Scanner Interface


```csharp

public interface IRegistryScanner

{

    Task<IEnumerable<RegistryIssue>>

    ScanAsync(

    CancellationToken token);


}

```



---

# RegistryIssue Model


Contains:


```

Id


RegistryPath


KeyName


Value


Category


RiskLevel


Recommendation


CanRepair


```



---

# Registry Rule Engine


Purpose:


Evaluate registry entries.



---

# Rule Structure


Every rule contains:


```

Condition


↓

Analysis


↓

Risk


↓

Action


```



---

# Example Rule


Invalid Path Rule:



Condition:


File does not exist.



Analysis:


Registry points to missing executable.



Risk:


Low.



Action:


Recommend removal.



---

# Risk Evaluation System


Registry items receive risk scores.



---

# Risk Factors


Consider:


Registry Location


↓

Entry Type


↓

System Dependency


↓

Application Relationship


↓

User Impact



---

# Risk Levels



## Safe


Example:


Old temporary application entries.



---

## Low Risk


Example:


Missing software paths.



---

## Medium Risk


Example:


Unused file associations.



---

## High Risk


Example:


Startup-related entries.



---

## Critical


Example:


System configuration entries.



---

# Automatic Repair Rules


Only:


Safe


↓

Low Risk


items can be automatically repaired.



---

# Medium Risk


Require:


User confirmation.



---

# High / Critical


Default:


Do not modify.



---

# Registry Backup System


Every modification requires backup.



---

# Backup Process


```

Select Registry Items


↓

Export Original Data


↓

Save Backup File


↓

Record Database Information


↓

Execute Modification


```



---

# Backup Format


Recommended:


```

.reg


```



---

# Backup Storage


Example:


```

ProgramData

└── LeslieCleaner

    └── Backup

        └── Registry


```



---

# Backup Naming


Format:


```

RegistryBackup_Date_Time.reg


```



---

# Restore System


Purpose:


Allow user recovery.



---

# Restore Flow


```

Select Backup


↓

Verify Backup


↓

Confirm Restore


↓

Import Registry


↓

Restart if Required


```



---

# Restore Safety


Before restore:


Create current backup.



---

# Registry Modification Executor


Responsible:


Execute approved changes.



---

# Executor Rules


Before modification:


1.

Check backup exists


2.

Check permission


3.

Check risk level


4.

Log operation


5.

Execute


6.

Verify result



---

# Operation Logging


Every registry change records:


```

Time


User Action


Registry Path


Old Value


New Value


Result


```



---

# User Interface Integration


Registry module must provide:


Scan progress


↓

Issue list


↓

Risk explanation


↓

Backup status


↓

Repair result



---

# Registry Cleaner Final Principle


A professional registry cleaner is not the one that deletes the most entries.


It is the one that:

Finds meaningful problems.

↓

Explains risks clearly.

↓

Changes safely.

↓

Can always recover.



---
---

# Registry Snapshot System


## Purpose


Provide a complete state record before risky operations.



---

# Snapshot Usage


Used for:


Major registry repair


↓

Batch modification


↓

System optimization operations



---

# Snapshot Creation Flow


```

User Starts Repair


↓

Analyze Selected Entries


↓

Create Snapshot


↓

Verify Snapshot


↓

Allow Modification


```



---

# Snapshot Content


Contains:


```

Registry Path


↓

Key Name


↓

Original Value


↓

Value Type


↓

Timestamp


↓

Operation Source


```



---

# Snapshot Storage


Recommended:


SQLite database + .reg backup file.



---

# Snapshot Database Relationship


Relationship:


```

RegistryBackups


        |

        |

RegistryBackupItems


```



---

# RegistryBackupItems Table


Structure:


```

Id


BackupId


RegistryPath


ValueName


OldValue


ValueType


CreatedTime


```



---

# Registry Scan Optimization


Registry scanning must be fast.



---

# Optimization Methods


Use:


Selective scanning


↓

Rule-based detection


↓

Cache


↓

Incremental analysis



---

# Avoid Full Registry Blind Scan


Forbidden:


Scan all registry keys every time.



---

# Correct Approach:


```

Known Risk Locations


↓

Targeted Rules


↓

Analyze Relevant Entries


```



---

# Registry Cache System


Purpose:


Avoid repeated analysis.



---

# Cache Content


Store:


```

Registry Path


Last Result


Risk Level


Scan Time


```



---

# Cache Expiration


Recommended:


7 days.



---

# Incremental Scan


If:


Registry unchanged.



Then:


Reuse previous analysis.



---

# False Positive Prevention


Critical requirement.



---

# Detection Confirmation


Before marking invalid:



Check:


```

Path Exists


↓

Application Exists


↓

File Signature


↓

Publisher Information


↓

Recent Usage


```



---

# Example


Registry:


```

OldApp.exe


```



Cannot immediately delete.



Need:


Check file existence.



---

# Publisher Verification


Use:


Digital Signature


↓

File Metadata


↓

Installation Records



---

# Application Relationship Analysis


Connect registry entries with:


Applications table.



---

# Example


Registry:


```

Steam Startup Entry


```



Check:


Applications table:


```

Steam Installed


```



Decision:


Keep.



---

# Repair Validation System


After repair:


Verify result.



---

# Validation Steps


```

Execute Modification


↓

Read Registry Again


↓

Confirm Change


↓

Generate Report


```



---

# Failed Repair Handling


If failed:


```

Stop Operation


↓

Restore Backup


↓

Record Error


```



---

# Registry Repair Report


Contains:


```

Repair Time


↓

Modified Entries


↓

Successful Count


↓

Failed Count


↓

Restoration Information


```



---

# User Confirmation System


Different risk levels require different confirmation.



---

# Safe


Automatic option:



```

Allow One Click Repair


```



---

# Low Risk


Default:


Show confirmation.



---

# Medium Risk


Require:


Detailed explanation.



---

# High Risk


Require:


Advanced confirmation.



---

# Critical


Not automatically repairable.



---

# Batch Registry Repair


Support:


```

Select All


Select None


Invert Selection


Batch Repair


```



---

# Batch Safety


Before batch operation:


Check every item individually.



---

# Registry Module Performance Targets


Scan:


<10 seconds


for normal systems.



---

# Memory Usage


Registry scan:


<100MB additional memory.



---

# Thread Model


Use:


Async Task



Avoid:


Blocking registry operations.



---

# Exception Handling


Handle:


```

Access Denied


↓

Invalid Key


↓

Registry Locked


↓

Permission Failure


```



---

# Logging Requirements


Record:


```

Scan Started


Scan Completed


Repair Started


Repair Completed


Failure


Restore


```



---

# Security Restrictions


Registry cleaner must not:


Modify security settings.


↓

Disable antivirus.


↓

Change System protection.


↓

Modify boot configuration.



---

# Testing Requirements


Must test:



## Normal Registry


Common software entries.



---

## Broken Entries


Missing applications.



---

## Protected Entries


System critical keys.



---

## Permission


Standard user.


↓

Administrator.



---

# Final Registry Module Requirement


The Registry Cleaner must be:


Safe


↓

Transparent


↓

Recoverable


↓

Accurate


↓

Performance Optimized



END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

