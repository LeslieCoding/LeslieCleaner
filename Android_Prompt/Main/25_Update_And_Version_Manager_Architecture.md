# LeslieCleaner Android Development Bible - 25_Update_And_Version_Manager_Architecture

> Android Prompt ID: AP-MAIN-25-UPDATE-AND-VERSION-MANAGER-ARCHITECTURE-MD  
> Source Prompt: `Main\25_Update_And_Version_Manager_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-25-UPDATE-AND-VERSION-MANAGER-ARCHITECTURE-MD` in the Android traceability matrix.
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

Update And Version Manager Architecture Specification


Priority:

★★★★☆



---

# Purpose


This document defines the architecture and update management standards of the LeslieCleaner version management system.


The update manager is responsible for:


Version detection


↓

Update package management


↓

Secure installation


↓

Version migration


↓

Rollback recovery



---

# Core Design Philosophy


Software updates must be:


Safe


↓

Transparent


↓

Recoverable


↓

Verifiable



---

# Main Goals


The update system must provide:


Reliable version detection


↓

Secure update delivery


↓

Complete rollback capability


↓

Minimal user interruption


↓

Data protection



---

# Architecture Overview


```

Update Manager


        |

        |

+----------------+

Version Checker

+----------------+

        |

        |

+----------------+

Update Resolver

+----------------+

        |

        |

+----------------+

Package Manager

+----------------+

        |

        |

+----------------+

Installer Engine

+----------------+

        |

        |

+----------------+

Rollback Manager

+----------------+


```



---

# Module Structure


```

Update


|

├── Version


├── Checker


├── Resolver


├── Downloader


├── Validator


├── Installer


├── Migration


├── Rollback


└── Report


```



---

# Version Management System


## Purpose


Manage LeslieCleaner software versions.



---

# Version Format


Use:


Semantic Versioning



Format:


```

Major.Minor.Patch


```



Example:


```

1.5.3


```



---

# Version Meaning



## Major


Large architecture changes.



Example:


```

1.x.x

↓

2.x.x


```



---

## Minor


New features.



Example:


```

1.4.x

↓

1.5.x


```



---

## Patch


Bug fixes.



Example:


```

1.5.1

↓

1.5.2


```



---

# Version Model


```csharp

public class ApplicationVersion

{

    public string Version { get; set; }


    public string BuildNumber { get; set; }


    public DateTime ReleaseDate { get; set; }


    public string Channel { get; set; }


}


```



---

# Update Channels


Support:



## Stable


Recommended version.



---

## Beta


Testing version.



---

## Development


Internal testing.



---

# Default Channel


Normal users:


```

Stable


```



---

# Version Checker


## Purpose


Detect available updates.



---

# Checker Interface


```csharp

public interface IVersionChecker

{

    Task<ApplicationVersion>

    CheckAsync();


}

```



---

# Version Check Flow


```

Current Version


↓

Connect Update Source


↓

Compare Version


↓

Return Result


```



---

# Update Source


Support:



## Local Source


Example:


```

Offline Update Package


```



---

## Remote Source


Example:


```

Update Server


```



---

# Update Metadata


Server provides:


```

Latest Version


↓

Release Notes


↓

Package Information


↓

Hash


↓

Signature


```



---

# Update Metadata Example


```json

{

"version":"1.6.0",

"package":"LeslieCleaner.zip",

"sha256":"xxxx",

"mandatory":false

}


```



---

# Update Detection Rules


Compare:



```

Current Version


VS


Latest Version


```



---

# Update Result


Possible states:



```

No Update


↓

Optional Update


↓

Mandatory Update


↓

Unsupported Version


```



---

# Mandatory Update


Used for:


```

Critical Security Fix


↓

Database Migration Requirement


↓

Major Compatibility Issue


```



---

# Update Resolver


## Purpose


Determine update strategy.



---

# Update Types



## Full Update


Contains:


```

Complete Application Files


```



---

## Incremental Update


Contains:


```

Changed Files Only


```



---

# Full Update Usage


Suitable:


```

Major Version Change


↓

First Installation


↓

Corrupted Installation


```



---

# Incremental Update Usage


Suitable:


```

Small Version Upgrade


↓

Bug Fix


↓

Performance Patch


```



---

# Update Decision Logic


Example:


```

Version Difference


↓

Package Size


↓

Network Condition


↓

Select Update Method


```



---

# Update Package Structure


```

UpdatePackage


|

├── Files


├── Manifest


├── Migration


├── Signature


└── Changelog


```



---

# Update Manifest


Contains:


```

Target Version


↓

Required Version


↓

File List


↓

Hash List


↓

Migration Script


```



---

# File List Example


```json

{

"files":[

{

"path":"LeslieCleaner.exe",

"hash":"xxxx"

}

]

}


```



---

# Package Manager


## Responsibility


Manage update files.



---

# Package Manager Functions


```

Download


↓

Extract


↓

Verify


↓

Prepare


```



---

# Temporary Update Directory


Recommended:


```

LeslieCleaner


|

└── UpdateTemp


```



---

# Download Safety


During download:


Verify:


```

File Size


↓

Hash


↓

Network Complete


```



---

# Interrupted Download


Support:


```

Resume Download


```



---

# Download Cache


Store:


```

Downloaded Packages


```



---

# Cache Cleanup


Remove:


```

Expired Packages


↓

Failed Packages


```
---

# Update Validation System


## Purpose


Ensure update packages are complete and trustworthy before installation.



---

# Validation Pipeline


```

Receive Package


↓

Check File Integrity


↓

Verify Manifest


↓

Verify Signature


↓

Check Compatibility


↓

Allow Installation


```



---

# Manifest Validation


Check:



```

Package Version


↓

Required Version


↓

File List


↓

Dependency Information


```



---

# File Integrity Verification


Each file contains:


```

Path


↓

Size


↓

Hash


```



---

# Hash Algorithm


Recommended:


```

SHA-256


```



---

# Verification Example


```

Downloaded File


        |

        ↓


Calculate SHA-256


        |

        ↓


Compare Manifest


        |

        ↓


Pass / Fail


```



---

# Digital Signature System


## Purpose


Prevent malicious update packages.



---

# Signature Contents


Sign:


```

Package Manifest


↓

File Hash List


↓

Version Information


```



---

# Signature Verification Flow


```

Download Package


↓

Read Signature


↓

Verify Public Key


↓

Check Signature


↓

Trust Package


```



---

# Signature Failure Handling


If failed:



```

Block Installation


↓

Delete Temporary Package


↓

Generate Security Log


```



---

# Compatibility Check


Before update:



Check:


```

Current Version


↓

Operating System


↓

Architecture


↓

Dependencies


```



---

# Unsupported Update Example


Current:


```


```



Package:


```


```



Result:


Reject update.



---

# Update Installer Engine


## Purpose


Install verified update packages.



---

# Installation Strategy


Use:


```

Staged Replacement


```



---

# Installation Flow


```

Create Backup


↓

Stop Application


↓

Replace Files


↓

Run Migration


↓

Start Application


↓

Verify


```



---

# Application Shutdown


Before update:


Save:


```

User Settings


↓

Runtime State


↓

Database Connection


```



---

# File Replacement Strategy


Never:


Direct overwrite important files.



---

# Correct:


```

Old File


↓

Backup


↓

Replace


↓

Verify


```



---

# Locked File Handling


If file is locked:


Options:


```

Retry


↓

Schedule Replacement


↓

Restart Required


```



---

# Restart Update Mode


For locked files:



```

System Startup Task


↓

Replace Files


↓

Remove Task


```



---

# Database Migration System


## Purpose


Maintain compatibility between versions.



---

# Migration Example


Version:


```

1.5.0


```



Database:


```

Old Structure


```



Update:


```

1.6.0


```



Migration:


```

Add New Column


```



---

# Migration Model


```csharp

public class MigrationStep

{

    public string FromVersion { get; set; }


    public string ToVersion { get; set; }


    public string Script { get; set; }


}

```



---

# Migration Rules


Must support:


```

Forward Migration


↓

Migration Verification


↓

Rollback Plan


```



---

# Migration Failure Handling


If failed:


```

Stop Update


↓

Restore Database Backup


↓

Rollback Version


```



---

# Automatic Update System


## Purpose


Provide convenient upgrades.



---

# Update Modes


Support:



## Manual


User checks update.



---

## Notify


System informs user.



---

## Automatic


System downloads and installs.



---

# Default Mode


Recommended:


```

Notify


```



---

# Automatic Update Restrictions


Never update during:


```

Running Cleanup


↓

Uninstall Operation


↓

Backup Operation


```



---

# Update Schedule


User configurable:



```

Startup Check


↓

Daily Check


↓

Weekly Check


```



---

# Bandwidth Control


Support:


```

Download Limit


↓

Pause


↓

Resume


```



---

# Update Notification


Display:


```

New Version


↓

Changes


↓

Size


↓

Restart Requirement


```



---

# Rollback Manager


## Purpose


Recover from failed updates.



---

# Rollback Principle


Every update must be reversible.



---

# Rollback Flow


```

Detect Failure


↓

Stop Application


↓

Restore Backup


↓

Restore Database


↓

Start Previous Version


```



---

# Rollback Backup Contents


Include:



```

Executable Files


↓

Libraries


↓

Configuration


↓

Database


```



---

# Rollback Trigger


Automatic:



```

Startup Failure


↓

Migration Failure


↓

File Verification Failure


```



---

# Manual Rollback


User can select:


```

Previous Version


```



---

# Update History


Store:



```

Version


↓

Update Time


↓

Result


↓

Rollback Status


```



---

# Update History Database


Table:


```

UpdateRecords


```



Structure:


```

Id


OldVersion


NewVersion


Status


CreatedTime


Error


```



---

# Update Report


After completion:


Generate:


```

Previous Version


↓

New Version


↓

Changed Files


↓

Migration Result


↓

Restart Requirement


```



---

# Logging System


Record:


```

Check Update


↓

Download


↓

Verification


↓

Installation


↓

Rollback


```



---

# Error Handling


Handle:


```

Network Failure


↓

Hash Error


↓

Permission Error


↓

Disk Space Error


↓

Migration Error


```



---

# Performance Requirements



## Version Check


<3 seconds



---

## Package Verification


Depends on package size.



---

## Installation


Minimal interruption.



---

# Security Requirements


Update system must never:


Install unsigned packages


↓

Execute unknown scripts


↓

Modify unrelated files


↓

Bypass user permission



---

# Testing Requirements



## Update Test


Test:


Old version → New version



---

## Rollback Test


Test:


Failed update recovery



---

## Migration Test


Test:


Database compatibility



---

## Security Test


Test:


Modified package detection



---

## Network Test


Test:


Interrupted downloads



---

# Update Manager Final Requirement


The update system must provide:


Secure upgrades


↓

Reliable migration


↓

Complete rollback


↓

Transparent process


↓

Long-term maintainability



END OF DOCUMENT


---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

