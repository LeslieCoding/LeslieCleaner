# LeslieCleaner Android Development Bible - 26_Logging_And_Monitoring_Architecture

> Android Prompt ID: AP-MAIN-26-LOGGING-AND-MONITORING-ARCHITECTURE-MD  
> Source Prompt: `Main\26_Logging_And_Monitoring_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-26-LOGGING-AND-MONITORING-ARCHITECTURE-MD` in the Android traceability matrix.
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

Logging And Monitoring Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the complete logging, monitoring, and diagnostic architecture of LeslieCleaner.


The logging system provides:


Operation tracking


↓

Error diagnosis


↓

Performance monitoring


↓

Security auditing


↓

System troubleshooting



---

# Core Design Philosophy


Logs are the foundation of software reliability.



The logging system must provide:



```

Complete Recording


↓

Easy Analysis


↓

Low Performance Impact


↓

Privacy Protection


```



---

# Main Goals


The logging system must achieve:


Accurate event tracking


↓

Fast problem diagnosis


↓

Long-term stability analysis


↓

Developer debugging support



---

# Architecture Overview


```

Logging System


        |

        |

+----------------+

Logger Core

+----------------+

        |

        |

+----------------+

Log Providers

+----------------+

        |

        |

+----------------+

Storage Engine

+----------------+

        |

        |

+----------------+

Analyzer

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

Logging


|

├── Core


├── Provider


├── Formatter


├── Storage


├── Monitor


├── Analyzer


├── Report


└── Cleanup


```



---

# Logging Core


## Purpose


Provide unified logging interface.



---

# Logger Interface


```csharp

public interface ILogger

{

    void Debug(string message);


    void Info(string message);


    void Warning(string message);


    void Error(string message);


    void Critical(string message);


}


```



---

# Log Levels


The system supports:



---

# DEBUG


Purpose:


Developer troubleshooting.



Examples:


```

Variable Values


↓

Execution Flow


↓

Detailed Information


```



---

# INFO


Purpose:


Normal operation records.



Examples:


```

Scan Started


↓

Cleanup Completed


↓

Backup Created


```



---

# WARNING


Purpose:


Potential problems.



Examples:


```

Permission Missing


↓

File Locked


↓

Unknown Item


```



---

# ERROR


Purpose:


Operation failures.



Examples:


```

Database Error


↓

Network Failure


↓

Module Failure


```



---

# CRITICAL


Purpose:


System-level failures.



Examples:


```

Application Crash


↓

Data Corruption


↓

Security Failure


```



---

# Log Entry Model


```csharp

public class LogEntry

{

    public long Id { get; set; }


    public string Level { get; set; }


    public string Module { get; set; }


    public string Message { get; set; }


    public DateTime Time { get; set; }


    public string Exception { get; set; }


}


```



---

# Log Context Information


Every important log should include:



```

Module Name


↓

Operation Name


↓

User Action


↓

Execution Result


```



---

# Example


```

[INFO]


Module:

Cleaner


Action:

Cache Cleanup


Result:

Success


```



---

# Module Logging Rules


Every module must have independent logging.



---

# Required Modules


Include:


```

Cleaner


↓

Startup Manager


↓

Privacy Cleaner


↓

File Analyzer


↓

Backup System


↓

Update Manager


↓

AI Assistant


```



---

# Logging Flow


```

Application Event


↓

Logger


↓

Formatter


↓

Storage


↓

Analyzer


```



---

# Log Formatter


## Purpose


Standardize log output.



---

# Log Format


Recommended:



```

Timestamp


+

Level


+

Module


+

Message


+

Context


```



---

# Example


```

2026-07-26


INFO


StartupManager


Scan completed


Items:25


```



---

# Storage Architecture


Supports:



## Local File Storage


Recommended.



Format:


```

.log


```



---

## Database Storage


Used for:


```

Search


↓

Statistics


↓

Analysis


```



---

# Log Directory


Recommended:


```

LeslieCleaner


|

└── Logs


    |

    ├── Application


    |

    ├── Error


    |

    └── Debug


```



---

# Log Rotation


## Purpose


Prevent unlimited growth.



---

# Rotation Rules


Based on:



## Size


Example:


```

100MB


```



---

## Time


Example:


```

30 Days


```



---

# Rotation Process


```

Check Size


↓

Archive Old Logs


↓

Create New Log


```



---

# Log Compression


Old logs may be compressed:



```

ZIP


```



---

# Log Retention Policy


Default:


```

Keep 30 Days


```



---

# User Configuration


Allow:


```

7 Days


↓

30 Days


↓

90 Days


↓

Unlimited


```



---

# Exception Logging


## Purpose


Capture errors completely.



---

# Exception Information


Include:


```

Exception Type


↓

Message


↓

Stack Trace


↓

Module


↓

Operation


```



---

# Example


```

NullReferenceException


Module:

BackupManager


Operation:

CreateBackup


```



---

# Exception Handling Rules


Never:


```

Ignore Exceptions


```



---

# Correct:


```

Catch


↓

Log


↓

Recover


↓

Notify


```---

# Performance Monitoring System


## Purpose


Monitor LeslieCleaner runtime performance and system resource usage.



---

# Monitoring Targets


The system monitors:



## CPU


Track:


```

Process CPU Usage


↓

Module CPU Usage


↓

Peak Usage


```



---

## Memory


Track:


```

Working Set


↓

Private Memory


↓

Memory Growth


```



---

## Disk


Track:


```

Read Speed


↓

Write Speed


↓

Disk Queue


```



---

## Network


Optional.



Track:


```

Download Speed


↓

Upload Speed


↓

API Request Status


```



---

# Performance Monitor Architecture


```

Performance Collector


        |

        |

System API


        |

        |

Data Processor


        |

        |

Performance Database


        |

        |

Report Generator


```



---

# Performance Data Model


```csharp

public class PerformanceRecord

{

    public long Id { get; set; }


    public string ProcessName { get; set; }


    public double CpuUsage { get; set; }


    public long MemoryUsage { get; set; }


    public long DiskUsage { get; set; }


    public DateTime Time { get; set; }

}

```



---

# Monitoring Interval


Default:


```

5 seconds


```



---

# Low Frequency Mode


When idle:


```

30 seconds


```



---

# High Performance Mode


During operations:


```

1 second


```



---

# Performance Alert System


## Purpose


Detect abnormal behavior.



---

# Alert Rules



## CPU Alert


Example:


```

CPU > 90%


持续 30 秒


```



---

## Memory Alert


Example:


```

Memory Increase


+

Continuous Growth


```



---

## Disk Alert


Example:


```

Disk Operation Abnormal


```



---

# Alert Levels



## Notice


Small performance changes.



---

## Warning


Potential performance issue.



---

## Critical


Application stability risk.



---

# Operation Audit System


## Purpose


Record important user operations.



---

# Audit Scope


Include:



```

Cleanup


↓

Restore


↓

Backup


↓

Update


↓

Settings Change


```



---

# Audit Record Model


```csharp

public class AuditRecord

{

    public long Id { get; set; }


    public string Action { get; set; }


    public string Module { get; set; }


    public string Result { get; set; }


    public DateTime Time { get; set; }


}

```



---

# Audit Example


```

Action:


Delete Cache


Module:


Cleaner


Result:


Success


Time:


2026-07-26


```



---

# Audit Purpose


Used for:


```

Problem Tracking


↓

User Review


↓

Security Analysis


```



---

# User Behavior Recording


## Purpose


Improve user experience.



---

# Allowed Data


Only record:


```

Feature Usage


↓

Operation Frequency


↓

Preference Settings


```



---

# Forbidden Data


Never record:


```

Personal Files


↓

Browser Content


↓

Passwords


↓

Private Information


```



---

# Behavior Statistics


Example:



```

Most Used Feature:


Cleaner


```



```

Most Frequent Operation:


Quick Scan


```



---

# Debug Mode System


## Purpose


Provide advanced troubleshooting.



---

# Debug Mode Levels



## Normal


Standard logs.



---

## Detailed


More operation information.



---

## Developer


Complete diagnostic information.



---

# Debug Configuration


Example:


```json

{

"level":"Detailed",

"savePerformance":true

}


```



---

# Debug Mode Restrictions


Developer logs may contain:



```

Technical Information


```



but must not contain:



```

Private User Data


```



---

# Crash Analysis System


## Purpose


Analyze application crashes.



---

# Crash Data


Include:


```

Exception


↓

Stack Trace


↓

Application Version


↓

System Information


```



---

# Crash Report Example


```

Application:


LeslieCleaner


Version:


1.5.0


Error:


NullReferenceException


Module:


Scanner


```



---

# Crash Storage


Directory:


```

Logs


|

└── Crash


```



---

# Crash Recovery


After crash:


```

Capture Error


↓

Save Report


↓

Restart Protection


↓

Notify User


```



---

# Diagnostic Report Generator


## Purpose


Generate complete troubleshooting package.



---

# Report Content


Include:


```

System Information


↓

Application Version


↓

Recent Logs


↓

Performance Data


↓

Crash Records


```



---

# Export Format


Support:


```

ZIP


↓

TXT


↓

JSON


```



---

# Privacy Protection


Before export:


Remove:


```

Personal Paths


↓

Private Data


↓

Sensitive Information


```



---

# Database Tables



## Logs


Structure:


```

Id


Level


Module


Message


Time


```



---

## PerformanceRecords


Structure:


```

Id


Cpu


Memory


Disk


Time


```



---

## AuditRecords


Structure:


```

Id


Action


Result


Time


```



---

# Log Security


Logs must:


Prevent tampering


↓

Maintain integrity


↓

Protect privacy



---

# Log Integrity Verification


Optional:


Use:


```

Hash Chain


```



---

# Example:


```

Log1 Hash


↓

Log2 Includes Log1 Hash


↓

Log3 Includes Log2 Hash


```



---

# Performance Optimization



## Async Logging


Use:


```

Background Queue


```



---

## Batch Writing


Avoid:


Frequent disk writes.



---

## Log Level Control


Disable:


Debug logs in production.



---

# Testing Requirements



## Logging Test


Verify:


All modules generate logs.



---

## Exception Test


Verify:


Errors recorded.



---

## Performance Test


Verify:


Monitoring overhead.



---

## Rotation Test


Verify:


Old logs cleanup.



---

## Privacy Test


Verify:


No sensitive data leakage.



---

# Logging And Monitoring Final Requirement


The system must provide:


Complete visibility


↓

Fast diagnosis


↓

Reliable monitoring


↓

Privacy protection


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

