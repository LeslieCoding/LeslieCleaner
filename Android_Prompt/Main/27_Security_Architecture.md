# LeslieCleaner Android Development Bible - 27_Security_Architecture

> Android Prompt ID: AP-MAIN-27-SECURITY-ARCHITECTURE-MD  
> Source Prompt: `Main\27_Security_Architecture.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: Android permission, profile, browser-provider and privacy-safe data handling with least privilege  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **Android permission, profile, browser-provider and privacy-safe data handling with least privilege**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-MAIN-27-SECURITY-ARCHITECTURE-MD` in the Android traceability matrix.
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

Security Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the complete security architecture of LeslieCleaner.


The security system provides:


Application protection


↓

User data protection


↓

Operation safety


↓

Integrity verification


↓

Attack prevention



---

# Core Design Philosophy


Security must be built into the architecture from the beginning, not added afterward. :contentReference[oaicite:0]{index=0}


LeslieCleaner follows:


```

Security By Design


↓

Least Privilege


↓

Defense In Depth


↓

Fail Safe


↓

User Control


```



---

# Main Goals


The security architecture must provide:


Data confidentiality


↓

Data integrity


↓

Operation safety


↓

Application reliability


↓

Privacy protection



---

# Security Architecture Overview


```

Security System


        |

        |

+----------------+

Security Manager

+----------------+

        |

        |

+----------------+

Permission Control

+----------------+

        |

        |

+----------------+

Encryption Service

+----------------+

        |

        |

+----------------+

Integrity Checker

+----------------+

        |

        |

+----------------+

Security Monitor

+----------------+


```



---

# Module Structure


```

Security


|

├── Permission


├── Authentication


├── Encryption


├── Integrity


├── Protection


├── Audit


├── Monitor


```



---

# Security Principles



## Least Privilege


Each module only receives required permissions.



Example:


Cleaner:


```

Read Temporary Files


```



Not allowed:


```

Modify System Files


```



---

## Defense In Depth


Multiple protection layers:



```

Permission Control


↓

Integrity Check


↓

Backup Recovery


↓

Audit Log


```



---

## Fail Safe


When errors occur:



Default behavior:


```

Stop Risky Operation


```



---

# Security Manager


## Purpose


Central security control component.



---

# Responsibilities


Include:


```

Permission Verification


↓

Security Policy


↓

Risk Detection


↓

Protection Actions


```



---

# Security Manager Interface


```csharp

public interface ISecurityManager

{

    bool CheckPermission(

        string permission);


    bool VerifyIntegrity(

        string file);


    SecurityResult AnalyzeRisk();


}

```



---

# Permission Management System


## Purpose


Control application access.



---

# Permission Types


Support:



## File Permission


Controls:


```

Read


↓

Write


↓

Delete


```



---

## Registry Permission


Controls:


```

Read


↓

Modify


↓

Delete


```



---

## Process Permission


Controls:


```

Start


↓

Stop


↓

Monitor


```



---

## Network Permission


Controls:


```

Connect


↓

Upload


↓

Download


```



---

# Permission Model


```csharp

public class PermissionInfo

{

    public string Name { get; set; }


    public string Category { get; set; }


    public bool Enabled { get; set; }


}


```



---

# Permission Checking Flow


```

Operation Request


↓

Security Manager


↓

Permission Check


↓

Allow / Deny


↓

Record Log


```



---

# Default Security Policy


Default:


```

Deny First


```



Only explicitly allowed operations can execute.



---

# Sensitive Operation Protection


Require confirmation:



```

Registry Modification


↓

System File Operation


↓

Mass Delete


↓

Update Installation


```



---

# Data Protection Architecture


## Purpose


Protect user and application data.



---

# Protected Data Categories


Include:


```

Configuration


↓

Database


↓

Backup


↓

API Credentials


↓

Security Information


```



---

# Data Classification



## Public


No protection required.



---

## Internal


Application data.



---

## Sensitive


Require protection.



Examples:


```

API Key


↓

Backup Metadata


```



---

## Critical


Highest protection.



Examples:


```

Encryption Key


↓

Authentication Secret


```



---

# Encryption System


## Purpose


Protect sensitive information.



---

# Encryption Usage



## Data At Rest


Protect:


```

Configuration


↓

Database


↓

Credentials


```



---

## Data In Transit


Protect:


```

API Communication


↓

Update Communication


```



---

# Encryption Algorithm


Recommended:



```

AES-256


```



---

# Hash Algorithm


Used for integrity:



```

SHA-256


```



---

# Encryption Service Interface


```csharp

public interface IEncryptionService

{

    byte[] Encrypt(

        byte[] data);


    byte[] Decrypt(

        byte[] data);


}

```



---

# Key Management


## Purpose


Secure encryption keys.



---

# Key Storage


Recommended:


```

System Credential Manager


↓

Protected Storage


```



---

# Never Store:


```

Plain Text Keys


↓

Source Code Keys


↓

Configuration Plain Keys


```



---

# Key Rotation


Support:


```

Generate New Key


↓

Re-encrypt Data


↓

Remove Old Key


```



---

# Security Startup Check


When application starts:



```

Verify Files


↓

Check Configuration


↓

Check Database


↓

Load Security Policy


```



---

# Startup Protection


If verification fails:



```

Stop Sensitive Functions


↓

Show Warning


↓

Generate Report


```



---

# Application Integrity Verification


## Purpose


Detect modification.



---

# Verification Targets


Include:


```

Executable Files


↓

Core Libraries


↓

Configuration Files


```



---

# Integrity Method


Use:


```

File Hash


↓

Signature Verification


```



---

# Integrity Check Flow


```

Application Start


↓

Calculate Hash


↓

Compare Expected Value


↓

Pass / Fail


```
---

# Anti-Tampering System


## Purpose


Prevent unauthorized modification of LeslieCleaner core files and important data.



---

# Protection Targets


Include:



```

Executable Files


↓

Core Libraries


↓

Configuration Files


↓

Database Files


↓

Plugin Metadata


```



---

# File Integrity Protection


Each protected file maintains:



```

File Path


↓

File Size


↓

SHA-256 Hash


↓

Verification Status


```



---

# Integrity Database


Table:


```

FileIntegrityRecords


```



Structure:


```

Id


FilePath


Hash


Size


Status


LastCheckTime


```



---

# Tampering Detection Flow


```

Application Start


↓

Load Integrity Records


↓

Calculate Current Hash


↓

Compare


↓

Pass / Reject


```



---

# Tampering Response



## Low Risk


Example:


Configuration modification.



Action:


```

Warning


↓

Log Event


```



---

## High Risk


Example:


Core executable modification.



Action:


```

Disable Sensitive Functions


↓

Require Repair


↓

Generate Report


```



---

# Secure Repair Mechanism


If core files are damaged:



```

Verify Backup


↓

Restore Original Files


↓

Recheck Integrity


```



---

# Plugin Security Protection


Plugins are considered external components.



---

# Plugin Verification


Before loading:



Check:


```

Manifest


↓

Signature


↓

Permission


↓

Integrity


```



---

# Unsafe Plugin Handling


If suspicious:



```

Block Loading


↓

Disable Plugin


↓

Record Security Event


```



---

# Security Audit System


## Purpose


Record security-related events.



---

# Audit Events


Include:



```

Permission Request


↓

Permission Denied


↓

File Integrity Failure


↓

Encryption Error


↓

Update Verification Failure


↓

Plugin Block


```



---

# Security Audit Model


```csharp

public class SecurityAuditRecord

{

    public long Id { get; set; }


    public string EventType { get; set; }


    public string Level { get; set; }


    public string Description { get; set; }


    public DateTime Time { get; set; }


}


```



---

# Audit Levels



## Information


Normal security activity.



---

## Warning


Potential risk.



---

## Critical


Security incident.



---

# Security Monitoring System


## Purpose


Continuously monitor application security status.



---

# Monitoring Items


Include:



```

Integrity Status


↓

Permission Status


↓

Plugin Status


↓

Encryption Status


↓

Update Status


```



---

# Security Health Score


Range:


```

0-100


```



---

# Score Factors



## Integrity


Weight:


30%



---

## Permission Safety


Weight:


20%



---

## Update Status


Weight:


20%



---

## Plugin Security


Weight:


15%



---

## Encryption Status


Weight:


15%



---

# Security Risk Engine


## Purpose


Evaluate possible security risks.



---

# Risk Factors


Include:


```

Outdated Version


↓

Unknown Plugin


↓

Modified Files


↓

Unsafe Configuration


```



---

# Risk Levels



## Safe


No action required.



---

## Notice


Recommended review.



---

## Warning


User attention required.



---

## Critical


Immediate action required.



---

# Security Recommendation Example


Problem:


```

Application version is outdated


```



Recommendation:


```

Update to latest version


```



---

Problem:


```

Unknown plugin detected


```



Recommendation:


```

Review plugin permissions


```



---

# Vulnerability Protection Strategy


## Purpose


Reduce common security risks.



---

# File Operation Protection


Prevent:



```

Unauthorized Delete


↓

Unexpected Modification


↓

Path Traversal


```



---

# Input Validation


All external input must:


```

Validate


↓

Filter


↓

Normalize


```



---

# Protected Path Rules


Prevent operations on:


```

System System Directory


↓

Application Core Directory


↓

Security Configuration


```



---

# Path Security


Check:



```

Absolute Path


↓

Symbolic Link


↓

Directory Boundary


```



---

# Network Security


For network communication:



Use:


```

HTTPS


↓

Certificate Verification


↓

Timeout Control


```



---

# API Security


Protect:


```

API Key


↓

Token


↓

Request Data


```



---

# API Request Rules


Include:


```

Authentication


↓

Rate Limit


↓

Error Handling


```



---

# Update Security


Before installing updates:



Verify:


```

Package Signature


↓

File Hash


↓

Source Trust


```



---

# Backup Security


Backup files must:



```

Maintain Integrity


↓

Prevent Unauthorized Access


↓

Support Verification


```



---

# Security Logging


Security logs must record:



```

Time


↓

Event


↓

Source


↓

Result


```



---

# Security Log Protection


Prevent:



```

Unauthorized Modification


↓

Silent Deletion


```



---

# Emergency Security Mode


## Purpose


Handle severe security issues.



---

# Trigger Conditions


Example:



```

Core File Modified


↓

Repeated Integrity Failure


↓

Security Verification Failed


```



---

# Emergency Mode Actions


```

Disable Risky Functions


↓

Protect Data


↓

Generate Diagnostic Report


↓

Request User Action


```



---

# Security Testing Requirements



## Permission Test


Verify:


Unauthorized operations blocked.



---

## Encryption Test


Verify:


Sensitive data protection.



---

## Integrity Test


Verify:


Modified files detected.



---

## Plugin Security Test


Verify:


Unsafe plugins blocked.



---

## Update Security Test


Verify:


Unsigned packages rejected.



---

## Recovery Test


Verify:


Security failure recovery works.



---

# Security Development Rules


Developers must:



```

Never Hardcode Secrets


↓

Validate External Input


↓

Use Secure APIs


↓

Log Security Events


↓

Review Permissions


```



---

# Security Architecture Final Requirement


The LeslieCleaner security system must provide:



Strong protection


↓

Safe operation


↓

Data confidentiality


↓

Integrity assurance


↓

Transparent auditing


↓

Long-term reliability



---

# LeslieCleaner Development Bible Completed


Document Series:


```

01 - 27 Architecture Documents


```



The complete software architecture now covers:



```

Core Architecture


↓

UI System


↓

Cleaning Engine


↓

Startup Manager


↓

Privacy System


↓

File Analyzer


↓

Backup System


↓

AI Assistant


↓

Plugin System


↓

Update System


↓

Logging System


↓

Security System


```



END OF DOCUMENT


---
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

