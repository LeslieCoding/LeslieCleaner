# LeslieCleaner Android Development Bible - 07_Release_Optimization

> Android Prompt ID: AP-MAIN-07-RELEASE-OPTIMIZATION-MD  
> Source Prompt: `Main\07_Release_Optimization.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-07-RELEASE-OPTIMIZATION-MD` in the Android traceability matrix.
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

Version

1.0

Document Type

Module Architecture

Priority

★★★★★

This document defines the detailed architecture of every major functional module in LeslieCleaner.

Every module must have:

Clear Responsibility

↓

Independent Architecture

↓

Defined Interfaces

↓

Independent Testing

↓

Performance Optimization

↓

Security Control


No module may bypass the architecture rules.

---

# Module Architecture Philosophy

LeslieCleaner uses:

Modular Architecture

+

Domain Driven Design

+

Service-Oriented Design

+

Plugin Extension Architecture


Each module communicates through:

Interfaces

↓

Application Services

↓

Events

↓

Commands


Direct module dependency is prohibited.

---

# Overall Module Architecture

```

                 LeslieCleaner UI


                        ↓


              Application Layer


                        ↓


 ------------------------------------------------

 Cleaner

 Registry

 Uninstaller

 Startup

 Privacy

 Browser

 Analyzer

 Shredder

 AI

 Plugin

 Update

 ------------------------------------------------


                        ↓


              Domain Services


                        ↓


        Infrastructure / Platform Layer

```

---

# Module Communication Rules

Allowed:

Module A

↓

Interface

↓

Module B


Example:

Cleaner

↓

IBackupService

↓

Backup Module


---

Forbidden:

Cleaner

↓

Registry Internal Class


Uninstaller

↓

Direct File Delete API


UI

↓

  Registry API


---

# Module Lifecycle Standard

Every module has:

Initialization

↓

Loading

↓

Ready

↓

Executing

↓

Completed

↓

Disposing


---

# Module Interface Standard

Every major module must provide:

IXXXService

IXXXScanner

IXXXAnalyzer

IXXXExecutor


Example:

Cleaner:

```
ICleanerService

ICleanScanner

ICleanExecutor
```

---

# Module Result Standard

All modules return unified result.


Example:

```
OperationResult<T>
{
    Success;

    Data;

    Message;

    ErrorCode;

    Duration;

    Statistics;
}
```

---

# Module Logging Requirement

Every module must provide:

Start Log

↓

Progress Log

↓

Completion Log

↓

Error Log


---

# Module Performance Requirement

Every module must support:

Async

↓

Cancellation

↓

Progress Reporting

↓

Memory Control


---

# Module Security Requirement

Every dangerous operation requires:

Risk Evaluation

↓

Permission Check

↓

Backup

↓

Confirmation

↓

Execution

↓

Rollback


---

# Cleaner Engine Architecture

## Purpose

Clean unnecessary files safely and efficiently.

---

# Cleaner Module Responsibilities

Includes:

Temporary Files

↓

Cache Files

↓

System Logs

↓

Application Junk

↓

Recycle Bin

↓

Update Residual Files


---

# Cleaner Does Not Handle

Registry Cleaning

↓

Software Uninstall

↓

Startup Management


These belong to independent modules.

---

# Cleaner Architecture


```

CleanerService


        ↓


CleanAnalyzer


        ↓


RuleEngine


        ↓


CleanExecutor


        ↓


Verification


```

---

# Cleaner Components

## Clean Scanner

Responsible:

Find candidate files.


Interface:

```
ICleanScanner
```


---

## Clean Analyzer

Responsible:

Determine:

Can Delete?

↓

Risk Level

↓

Expected Space


Interface:

```
ICleanAnalyzer
```

---

## Clean Executor

Responsible:

Execute deletion.


Interface:

```
ICleanExecutor
```

---

## Clean Verification

Responsible:

Confirm:

Deleted

↓

Space Released

↓

No Error


---

# Cleaner Rule System

Rules are data-driven.

Not hardcoded.

---

Rule Example:

```
RuleId:

WIN_TEMP_001


Name:

  Temporary Files


Path:

%TEMP%


Risk:

Safe


Action:

Delete

```

---

# Cleaner Workflow

```

User Start Scan


↓

Load Rules


↓

Parallel Scan


↓

Analyze Result


↓

Calculate Risk


↓

Display UI


↓

User Confirm


↓

Create Backup


↓

Execute Clean


↓

Verify


↓

Save History


```

---

# Cleaner Performance Optimization

Must use:

Known Location Index

↓

Incremental Scan

↓

Parallel Processing

↓

Cache

↓

Streaming


---

# Cleaner AI Enhancement

AI may provide:

Cleaning Suggestions

↓

Explain Files

↓

Recommend Optimization


AI must not directly delete.

---

# Registry Cleaner Architecture

## Purpose

Safely analyze and optimize registry.


---

# Registry Responsibilities

Detect:

Invalid Paths

↓

Missing References

↓

Broken Associations

↓

Unused Entries

↓

Application Residues


---

# Registry Architecture

```

RegistryScanner


↓

RegistryAnalyzer


↓

RiskEngine


↓

BackupService


↓

RegistryExecutor


↓

RestoreService


```

---

# Registry Scanner

Responsible:

Registry Enumeration.


Supported:

HKCU

↓

HKLM

↓

Classes

↓

Startup Keys


---

# Registry Analyzer

Analyze:

Reference Validity

↓

File Existence

↓

Application Status

↓

Risk


---

# Registry Risk Engine

Every item receives:

Risk Score:

0-100


Example:

0-30

Safe


31-70

Review


71-100

Dangerous


---

# Registry Backup System

Before modification:

Export affected keys.

Store:

Backup ID

↓

Timestamp

↓

Operation

↓

Original Value


---

# Registry Restore

Support:

Single Restore

↓

Batch Restore

↓

Automatic Rollback


---

# Registry Performance Optimization

Use:

Targeted Scan

↓

Known Patterns

↓

Cache

↓

Incremental Analysis


Avoid:

Full Registry Blind Scan


---

# Registry Safety Rules

Never delete:

  Core Keys

↓

Driver Configuration

↓

Security Components

↓

Unknown Critical Entries

---

# Application Uninstaller Module Architecture

## Purpose

The Uninstaller module is one of the core competitive features of LeslieCleaner.

The goal:

Provide a faster, safer and more complete uninstall experience than traditional uninstall tools.

The module must solve common problems:

Incomplete uninstall

↓

Remaining files

↓

Registry leftovers

↓

Startup residue

↓

Services not removed

↓

Scheduled tasks remaining

↓

Browser extensions remaining

↓

Configuration files remaining


---

# Uninstaller Architecture Overview


```

                Uninstaller Service


                        ↓


        Application Discovery Engine


                        ↓


        Application Fingerprint Engine


                        ↓


        Uninstall Controller


                        ↓


        Residual Detection Engine


                        ↓


        Cleanup Executor


                        ↓


        Verification Engine


                        ↓


        History Database


```


---

# Uninstaller Module Responsibilities

The module contains:


## Application Discovery

Find installed software.


Sources:

  Registry

↓

  Package Manager

↓

Program Files

↓

ProgramData

↓

AppData

↓

Start Menu

↓

Installation Metadata


---

## Application Analysis

Analyze:

Publisher

↓

Version

↓

Installation Location

↓

Size

↓

Installation Date

↓

Risk Level


---

## Uninstall Execution

Execute:

Official uninstall program

↓

Silent uninstall

↓

Custom uninstall


---

## Residual Cleanup

Detect and remove:

Files

↓

Folders

↓

Registry

↓

Services

↓

Startup Items

↓

Scheduled Tasks

↓

Extensions


---

# Application Discovery Engine


## Interface


```
IApplicationDiscoveryService
```


---

# Discovery Pipeline


```

Registry Scan


+

File System Analysis


+

Package Manager Query


+

Shortcut Analysis


↓

Application Database


```


---

# Application Database


Table:

Applications


Fields:


```

Id

Name

Publisher

Version

InstallPath

InstallDate

Size

UninstallCommand

ExecutableFiles

FingerprintId

RiskLevel

```


---

# Application Fingerprint Engine

## Purpose

Solve slow residual detection problem.

Traditional approach:

Search entire disk after uninstall.


Problem:

Very slow.


---

LeslieCleaner approach:


Before uninstall:

Create application fingerprint.


After uninstall:

Compare remaining data.


---

# Fingerprint Content


Each application stores:


## File Fingerprint


```

Executable Files

Installation Files

Configuration Files

Cache Locations

Log Locations

Update Files

```


---

## Registry Fingerprint


Store:


```

Registry Paths

Keys

Values

Associations

Services

Startup Entries

```


---

## System Integration Fingerprint


Include:


```

  Services

Scheduled Tasks

Context Menu

File Associations

Browser Extensions

```


---

# Fingerprint Generation Process


```

Application Detected


↓

Collect Metadata


↓

Analyze Installation


↓

Generate Fingerprint


↓

Store Local Database


↓

Ready For Fast Uninstall


```


---

# Residual Detection Engine


## Purpose

Detect leftovers after uninstall.


---

# Traditional Method Problem


```

Uninstall

↓

Scan Entire Disk

↓

Compare Names

↓

Search Registry

↓

Wait


```


Time:

Several minutes.


---

# LeslieCleaner Optimized Method


```

Load Fingerprint


↓

Check Known Locations


↓

Compare Snapshot


↓

Validate Residues


↓

Generate Result


```


Time Target:


<30 seconds


---

# Snapshot Difference System


Before uninstall:


Create:


```

File Snapshot

Registry Snapshot

Service Snapshot

Startup Snapshot

Task Snapshot

```


---

After uninstall:


Compare:


```

Before

+

After


=

Residual Report


```


---

# File Snapshot Optimization


Do not store full file contents.


Store:


```

Path

Size

Modified Time

Hash(Optional)

Type

```


---

# Registry Snapshot Optimization


Store:


```

Hive

Path

Key

Value

Type

```


---

# Residual Confidence Algorithm


Every residual receives score.


Example:


```

Path Match

+

Publisher Match

+

Hash Match

+

Registry Match

+

Location Match


=

Confidence Score

```


---

# Confidence Level


## High Confidence

90-100


Automatically recommend cleanup.


---

## Medium Confidence

60-90


Require user confirmation.


---

## Low Confidence

<60


Do not recommend.


---

# Residual Result UI Data


Each item:


```

Name

Location

Type

Size

Risk

Confidence

Reason


```


---

# Uninstall Execution Workflow


```

User Select Application


↓

Analyze Application


↓

Create Backup


↓

Start Official Uninstaller


↓

Monitor Changes


↓

Detect Residuals


↓

Show Result


↓

User Confirm


↓

Cleanup Residuals


↓

Verify


↓

Save History


```


---

# Backup System


Before uninstall:


Backup:


Files

↓

Registry

↓

Configuration


---

Backup Storage:


```

Backup ID

Application

Timestamp

Location

Size

Status

```


---

# Rollback Capability


Support:


Restore Registry

↓

Restore Files

↓

Restore Configuration


---

# Batch Uninstall Architecture


## Purpose

Allow users to uninstall multiple applications.


---

# Batch Workflow


```

Select Applications


↓

Create Queue


↓

Analyze Risk


↓

Execute Sequentially


↓

Collect Results


↓

Cleanup Residuals


```


---

# Batch Safety Rules


Before execution:


Show:


```

Selected:

15 Applications


Estimated Cleanup:

18GB


Estimated Time:

5 Minutes


Risk:

Medium


```


---

# Batch Operation Controls


Required:


Select All


↓

Select None


↓

Invert Selection


↓

Filter


↓

Pause


↓

Cancel


↓

Retry Failed


---

#   Built-in Application Management


## Purpose

Manage   pre-installed applications.


Examples:


Microsoft Store Apps

↓

Xbox Components

↓

OneDrive

↓

Weather

↓

Unused Features


---

# System Application Classification


Must separate:


## Microsoft Protected Apps


Examples:


  Security

↓

Core Components

↓

System Services


Default:


Protected


---

## Optional Microsoft Apps


Examples:


Games

↓

Optional Tools

↓

Unused Applications


Allow:

Disable

↓

Remove


---

# System App Safety Rules


Never automatically remove:


  Security

↓

Drivers

↓

System Shell

↓

Update Components


---

# Service Residual Cleanup


Detect:


```

Application Services

Update Services

Background Services


```


---

# Scheduled Task Cleanup


Detect:


```

Application Update Tasks

Telemetry Tasks

Maintenance Tasks


```


---

# Browser Extension Cleanup


Detect:


```

Chrome Extensions

Edge Extensions

Firefox Extensions


```


---

# Uninstaller Performance Requirements


Targets:


Application Discovery:

<5 seconds


Fingerprint Analysis:

<10 seconds


Residual Detection:

<30 seconds


Batch Analysis:

Parallel


---

# Uninstaller Memory Requirements


Normal Operation:


<250MB


Deep Residual Analysis:


<500MB


---

# Uninstaller Security Rules


Before deleting:


Validate ownership

↓

Check path

↓

Backup

↓

Confirm


---

# Uninstaller Module Final Requirement


The LeslieCleaner Uninstaller must provide:


Faster analysis

↓

More complete cleanup

↓

Safer deletion

↓

Better residual detection

↓

Lower resource usage


The goal is to overcome the biggest weakness of traditional PC cleaning software:

Slow and incomplete uninstall residue removal.

---
---

# Startup Manager Module Architecture

## Purpose

The Startup Manager is a critical optimization module.

Its primary goal:

Reduce   boot time

↓

Reduce unnecessary background processes

↓

Improve system responsiveness

↓

Prevent accidental disabling of critical   components


---

# Startup Manager Design Philosophy

Startup optimization must prioritize:

Safety

↓

Accuracy

↓

Transparency

↓

Performance


The user must always understand:

What starts automatically

↓

Why it starts

↓

Whether it is safe to disable

↓

What impact it has


---

# Startup Architecture Overview


```

                 Startup Manager Service


                         ↓


              Startup Discovery Engine


                         ↓


              Classification Engine


                         ↓


              Impact Analyzer


                         ↓


              Optimization Engine


                         ↓


              Backup & Restore System


```


---

# Startup Discovery Engine

## Purpose

Collect all startup-related entries.


Sources:


## Registry Startup


Locations:


``

```


---

## Startup Folder


Detect:


```

User Startup Folder

Common Startup Folder


```


---

##   Services


Analyze:


```

Automatic Services

Delayed Start Services

Background Services


```


---

## Scheduled Tasks


Detect:


```

Application Startup Tasks

Updater Tasks

Maintenance Tasks


```


---

## Drivers


Detect:


```

Kernel Drivers

Hardware Drivers


```


---

# Startup Item Data Model


```

StartupItem

{

Id

Name

Publisher

Location

Type

Path

Enabled

Impact

Category

RiskLevel

Recommendation

}

```


---

# Startup Classification Engine

## Core Requirement

Startup items must be separated into two major categories.


---

# Category 1

# Microsoft System Startup


Purpose:

Protect   stability.


Includes:


```

  Components

Microsoft Services

Security Components

System Drivers

Core Background Services


```


---

# Microsoft Classification Rules


Identify through:


Publisher


↓

Microsoft Corporation


↓

Digital Signature


↓

System Location


↓

Known   Database


---

# Microsoft Protection Level


Default:


Protected


---

Allowed Operations:


View Information


↓

Explain Function


↓

Advanced User Override


---

Forbidden:


One-click Disable All


↓

Automatic Removal


↓

Silent Modification


---

# Category 2

# User Installed Application Startup


Purpose:

Optimize boot speed.


Includes:


```

Third-party Applications

Auto Launchers

Update Services

Cloud Synchronization

Game Launchers

Communication Software


```


---

# User Application Classification


Identify:


Publisher != Microsoft


+

Installed Location


+

Digital Signature


+

Application Database


---

# Startup Impact Analyzer


Purpose:

Estimate boot impact.


---

# Impact Factors


Calculate:


Startup Delay


+

CPU Usage


+

Memory Usage


+

Disk Access


+

Network Activity


---

# Impact Level


## Low


Example:


Small background tools


---

## Medium


Example:


Cloud synchronization


---

## High


Example:


Large launchers

Auto update systems


---

# Startup Recommendation Engine


Generate:

Recommended Action


Examples:


```

Discord

Impact:

High


Recommendation:

Disable startup


Reason:

Runs background service after boot.


```


---

# Startup Optimization Engine


Supported actions:


## Disable Startup


Remove automatic launch.


---

## Enable Startup


Restore automatic launch.


---

## Delay Startup


Start application after   becomes stable.


---

## Remove Invalid Entry


Delete broken startup reference.


---

# Startup Delay Algorithm


Purpose:

Improve boot speed without disabling software.


---

Workflow:


```

  Boot


↓

Load Essential Components


↓

Wait System Stabilization


↓

Launch Delayed Applications


```


---

# Startup Backup System


Before modification:


Create backup:


```

Original Location

Original Value

Timestamp

Operation

User Action


```


---

# Startup Restore


Support:


Restore Single Item


↓

Restore Multiple Items


↓

Restore Previous State


---

# Startup Safety Rules


Never modify automatically:


  Defender


↓

Security Services


↓

Drivers


↓

System Shell


↓

  Update Components


---

# Startup Performance Target


Scanning:

<3 seconds


Analysis:

<5 seconds


Optimization:

Immediate


---

# Service Manager Module Architecture


## Purpose


Manage   services safely.


---

# Service Discovery


Collect:


```

Service Name

Display Name

Publisher

Status

Startup Type

Path


```


---

# Service Classification


Categories:


##   Core Services


Protected.


---

## Microsoft Optional Services


Reviewable.


---

## Third-party Services


Optimization candidates.


---

# Service Risk Engine


Evaluate:


```

Publisher

Digital Signature

Path

Dependency

Running Status


```


---

# Service Dependency Analysis


Before disabling:


Check:


Required By


↓

Depends On


↓

System Impact


---

# Service Operation Safety


Before modification:


Backup Configuration


↓

Create Restore Point


↓

Execute


↓

Verify


---

# File Analyzer Module Architecture


## Purpose


Analyze disk usage.


Functions:


Large File Detection


↓

Duplicate File Detection


↓

Old File Detection


↓

Disk Usage Visualization


---

# File Analyzer Architecture


```

File Index Engine


↓

Metadata Analyzer


↓

Classification Engine


↓

Visualization Layer


```


---

# File Index Engine


Collect:


```

Path

Size

Created Time

Modified Time

Extension

Hash(Optional)


```


---

# Large File Detection


Rules:


User configurable:


Example:


```

>500MB


```


---

# Duplicate Detection Architecture


Optimization:


Do not hash all files.


---

Pipeline:


```

File Size Grouping


↓

Extension Filtering


↓

Partial Hash


↓

Full Hash


↓

Duplicate Group


```


---

# Duplicate File Result


Display:


```

Original File

Duplicate Count

Total Waste

Recommendation


```


---

# Old File Detection


Analyze:


Modified Date


↓

Access Date


↓

Usage Pattern


---

# Privacy Cleaner Module Architecture


## Purpose


Remove privacy traces.


---

# Privacy Components


Includes:


Browser History


↓

Recent Documents


↓

Search History


↓

Temporary Records


↓

Application Usage Records


---

# Privacy Scanner Architecture


```

Privacy Collector


↓

Privacy Analyzer


↓

Risk Evaluator


↓

Cleaner


```


---

# Privacy Risk Model


Evaluate:


Sensitivity


↓

Age


↓

User Preference


↓

Recovery Possibility


---

# Privacy Protection Rules


Never automatically delete:


Passwords


↓

Authentication Tokens


↓

Important User Data


---

# Privacy Module Performance


Scan:

<10 seconds


Memory:

<120MB


---
---

# Browser Cleaner Module Architecture

## Purpose

Provide secure browser data cleanup.

Support:

Chrome

↓

Microsoft Edge

↓

Firefox

↓

Brave

↓

Opera

↓

Other Chromium Browsers


---

# Browser Module Architecture


```

Browser Discovery Service


↓

Profile Analyzer


↓

Data Scanner


↓

Privacy Risk Analyzer


↓

Cleanup Executor


↓

Verification


```


---

# Browser Discovery Service


Responsibilities:


Detect installed browsers.


Collect:


```

Browser Name

Version

Install Location

Profile Location

User Count


```


---

# Browser Profile Analyzer


Analyze:


```

Default Profile

Additional Profiles

Guest Profiles

Cache Location

Database Files


```


---

# Browser Data Scanner


Supported data:


## Cache


Detect:


```

Images

Scripts

Temporary Resources

Network Cache


```


---

## History


Detect:


```

Browsing History

Download History

Search Records


```


---

## Cookies


Important:


Cookies may contain:

Login Sessions

Authentication Data


Default:

Protected


---

## Session Data


Includes:


```

Open Tabs

Temporary Sessions


```


---

# Browser Privacy Protection


Before deletion:


Analyze:


Cookie Importance


↓

Domain


↓

User Preference


↓

Protection List


---

# Browser Cleanup Options


User can choose:


```

□ Cache

□ History

□ Cookies

□ Download Records

□ Session Data

□ Site Storage


```


---

# Browser Backup


Optional backup:


```

Browser Data Snapshot

Timestamp

Browser

Profile


```


---

# Browser Performance Target


Detection:

<5 seconds


Cleanup:

<20 seconds


---

# File Shredder Module Architecture


## Purpose


Provide secure irreversible file destruction.


---

# Shredder Architecture


```

Shredder Service


↓

Deletion Strategy Engine


↓

Secure Writer


↓

Verification Engine


↓

History Logger


```


---

# Supported Shredding Algorithms


## Quick Delete


Purpose:


Fast removal.


Method:


Remove filesystem reference.


---

## Standard Shred


Purpose:


General privacy protection.


Method:


Multiple overwrite.


---

## Advanced Shred


Purpose:


Sensitive files.


Method:


High-security overwrite strategy.


---

# Shredder Workflow


```

Select File


↓

Analyze Permission


↓

Confirm Operation


↓

Lock File


↓

Overwrite


↓

Delete


↓

Verify


↓

Record History


```


---

# Shredder Safety Rules


Before execution:


Check:


File Ownership

↓

File Lock Status

↓

System Protection


---

Never allow:

System critical file destruction.

---

# AI Intelligent Analysis Module Architecture


## Purpose


Provide intelligent assistance.

AI improves usability.

AI does not replace security decisions.


---

# AI Architecture


```

AI Assistant Service


↓

Context Analyzer


↓

Recommendation Engine


↓

Safety Filter


↓

Action Dispatcher


```


---

# AI Capabilities


## System Analysis


Example:


"Why is my computer slow?"


Analyze:


CPU

↓

Memory

↓

Startup

↓

Storage


---

## Cleaning Recommendation


Example:


"You have 20GB browser cache."


Provide:


Explanation

↓

Recommendation

↓

User Confirmation


---

## Startup Optimization Advice


Example:


"This application delays startup."


Explain:


Publisher

↓

Impact

↓

Recommendation


---

# AI Safety Layer


AI cannot:


Automatically delete files.


↓

Automatically modify registry.


↓

Disable security services.


↓

Execute dangerous commands.


---

# AI Action Flow


```

AI Suggestion


↓

Risk Evaluation


↓

User Confirmation


↓

Execute


↓

Record


```


---

# Plugin System Architecture


## Purpose


Allow future extension.


---

# Plugin Architecture


```

Plugin Manager


↓

Plugin Loader


↓

Plugin Sandbox


↓

Plugin Interface


↓

Plugin Service


```


---

# Plugin Interface


Every plugin must implement:


```

IPlugin


{

Initialize()

Start()

Stop()

Dispose()

}


```


---

# Plugin Metadata


Required:


```

Name

Version

Author

Description

Permission

Risk Level


```


---

# Plugin Security


Plugins require:


Permission Declaration


↓

Signature Verification


↓

Isolation


↓

Resource Limit


---

# Plugin Lifecycle


```

Install


↓

Verify


↓

Enable


↓

Run


↓

Disable


↓

Remove


```


---

# Update System Architecture


## Purpose


Provide safe software updates.


---

# Update Architecture


```

Update Checker


↓

Download Manager


↓

Package Validator


↓

Installer


↓

Rollback Manager


```


---

# Update Check


Check:


Version

↓

Release Notes

↓

Compatibility


---

# Update Security


Before installation:


Verify:


Digital Signature


↓

Hash


↓

Package Integrity


---

# Rollback System


If update fails:


Restore:

Previous Version

↓

Configuration

↓

Database


---

# Local Database Architecture


## Database Type


Recommended:


SQLite


---

# Database Responsibilities


Store:


Application Metadata


↓

Scan History


↓

Cleanup Records


↓

Backup Records


↓

Rules


↓

Cache


---

# Database Design Principles


Required:


Indexes


↓

Transactions


↓

Async Access


↓

Data Migration


---

# Core Database Tables


## Applications


Store installed software.


---

## ScanResults


Store scanning results.


---

## CleanupHistory


Store operations.


---

## BackupRecords


Store backups.


---

## Rules


Store cleaning rules.


---

## Fingerprints


Store application fingerprints.


---

# Event System Architecture


Modules communicate through events.


---

# Event Examples


```

ScanStartedEvent


ScanCompletedEvent


CleanupCompletedEvent


ApplicationRemovedEvent


StartupChangedEvent


```


---

# Event Rules


Events must be:


Lightweight


↓

Asynchronous


↓

Independent


---

# Configuration System Architecture


## Purpose


Centralized configuration.


---

# Configuration Includes:


User Preferences


↓

Performance Settings


↓

Security Settings


↓

Theme Settings


↓

Advanced Options


---

# Configuration Storage


Use:


Encrypted Local Configuration


---

# Complete Module Dependency Graph


```

                    UI Layer


                       ↓


              Application Services


                       ↓


 ------------------------------------------------


 Cleaner


 Registry


 Uninstaller


 Startup


 Privacy


 Browser


 Analyzer


 Shredder


 AI


 Plugin


 Update


 ------------------------------------------------


                       ↓


              Infrastructure Layer


                       ↓


   API


 File System


 Registry


 Database


 Network


```


---

# Architecture Final Requirements


Every module must:


✓ Independent

✓ Testable

✓ Async

✓ Secure

✓ Performant

✓ Maintainable

✓ Extensible


---

# Final Module Architecture Statement


LeslieCleaner is not a simple cleaning utility.

It is a modular   system optimization platform.


The architecture must support:

Current features

↓

Future expansion

↓

AI enhancement

↓

Plugin ecosystem

↓

Long-term maintenance


END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

