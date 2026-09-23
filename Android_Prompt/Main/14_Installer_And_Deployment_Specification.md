# LeslieCleaner Android Development Bible - 14_Installer_And_Deployment_Specification

> Android Prompt ID: AP-MAIN-14-INSTALLER-AND-DEPLOYMENT-SPECIFICATION-MD  
> Source Prompt: `Main\14_Installer_And_Deployment_Specification.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: APK/split-APK installation and uninstall through PackageInstaller with user confirmation and rollback-aware state  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **APK/split-APK installation and uninstall through PackageInstaller with user confirmation and rollback-aware state**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-MAIN-14-INSTALLER-AND-DEPLOYMENT-SPECIFICATION-MD` in the Android traceability matrix.
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

Installer And Deployment Specification


Priority:

★★★★★


---

# Purpose


This document defines the installation and deployment system of LeslieCleaner.


The goal:


Create a reliable personal System desktop software installation process.


LeslieCleaner is designed for:

Personal long-term usage


Therefore:


No commercial publishing system is required.


Focus on:


Easy installation

↓

Safe upgrade

↓

Clean uninstall

↓

Stable operation



---

# Deployment Philosophy


The installation system must be:


Simple

↓

Safe

↓

Customizable

↓

Easy to maintain



---

# Installer Technology Recommendation


Recommended:


## WiX Toolset


or


## Inno Setup


or


## NSIS



Preferred:


Inno Setup


Reason:


Simple

↓

Stable

↓

Supports custom paths

↓

Good System compatibility



---

# Installation Features Required


The installer must support:


✓ Custom installation location


✓ Administrator permission handling


✓ Desktop shortcut


✓ Start Menu shortcut


✓ Uninstaller registration


✓ Configuration preservation


✓ Upgrade installation


✓ Complete removal



---

# Installation Wizard Design


The installation process:


```

Welcome


↓

License / Information


↓

Installation Location


↓

Shortcut Options


↓

Install


↓

Finish


```



---

# Welcome Page


Display:


```

Welcome to LeslieCleaner Setup


Install LeslieCleaner on your computer.


```



---

# Installation Location Page


Critical requirement:


User can choose installation directory.



---

# Default Path


Recommended:


```
C:\Program Files\LeslieCleaner

```



---

# Custom Path Example


User can select:


```
D:\Software\LeslieCleaner


E:\Tools\LeslieCleaner


```



---

# Installation Directory Rules


Path must:


Exist or create


↓

Have write permission


↓

Have enough space



---

# Invalid Path Handling


Reject:


```

System system folders


↓

Root directory


↓

Protected locations


```



---

# Installation Permission Model


LeslieCleaner requires:


Administrator permission



Reason:


Need access:


Registry


↓

System Services


↓

Startup Management


↓

System Files



---

# Permission Flow


```

Start Installer


↓

Request Administrator Permission


↓

Verify Permission


↓

Continue Installation


```



---

# Installed File Structure


Recommended:


```
LeslieCleaner


│

├── LeslieCleaner.exe


├── Core


├── Modules


├── Resources


├── Config


├── Database


├── Logs


├── Backup


└── Plugins


```



---

# Directory Explanation


## Core


Contains:


Main runtime components.



---

## Modules


Contains:


Functional modules.


Example:


```
Cleaner

Registry

Startup

Uninstaller


```



---

## Resources


Contains:


```

Icons

Images

Language Files


```



---

## Config


Contains:


User configuration.



---

## Database


Contains:


SQLite database.



Example:


```
LeslieCleaner.db

```



---

## Logs


Contains:


Runtime logs.



---

## Backup


Contains:


Registry backup


↓

Cleanup backup


↓

Restore files



---

## Plugins


Contains:


Optional extensions.



---

# User Data Separation


Important:


Program files and user data should be separated.



---

# Recommended


Program:


```
C:\Program Files\LeslieCleaner

```



User Data:


```
C:\ProgramData\LeslieCleaner

```



---

# User Data Includes


```

Database


↓

Logs


↓

Backup


↓

Configuration


```



---

# Upgrade Strategy


When installing new version:


Do not delete:


```

Database


↓

Backup


↓

User Settings


```



---

# Upgrade Process


```

Detect Existing Installation


↓

Backup Configuration


↓

Replace Program Files


↓

Run Database Migration


↓

Verify


```



---

# Version Storage


Store:


```
ApplicationVersion


DatabaseVersion


```



---

# Installation Verification


After installation:


Check:


Executable exists


↓

Dependencies available


↓

Database initialized


↓

Configuration created


↓

Shortcut created



---

# Shortcut Creation


Options:


Desktop Shortcut


↓

Start Menu Shortcut



---

# Shortcut Properties


Include:


```

Name:

LeslieCleaner


Target:

LeslieCleaner.exe


Icon:

LeslieCleaner.ico


```



---

# First Launch Process


When first running:


```

Check Environment


↓

Check Permission


↓

Initialize Database


↓

Load Configuration


↓

Start Application


```



---

# Environment Check


Verify:


.NET Runtime


↓

System Version


↓

Required Permission


↓

Disk Space



---

# Missing Dependency Handling


If missing:


Show:


```

Required component is missing.

Please install required runtime.


```



---

# Uninstaller Design


LeslieCleaner must provide complete uninstall.



---

# Uninstall Entry


Register in:


System Apps & Features



---

# Uninstall Options


User chooses:


```

Remove Program Only


or


Remove Program + Data


```



---

# Program Only


Remove:


```

Executable

Modules

Resources


```



---

# Program + Data


Remove:


```

Database

Logs

Backup

Configuration


```



---

# Safety Confirmation


Before removing data:


Show:


```

This will permanently remove LeslieCleaner data.

Continue?


```



---

# Uninstall Process


```

Stop Application


↓

Stop Background Services


↓

Remove Files


↓

Remove Registry Entries


↓

Remove Shortcuts


↓

Cleanup


```



---

# Important


Uninstaller must not remove:


User personal files.



---

# Installation Logging


Installer records:


```

Installation Time


Install Path


Version


Result


```



---

# Deployment Final Requirement


The installation system must make LeslieCleaner:


Easy to install


↓

Easy to update


↓

Easy to remove


↓

Safe for long-term personal use


---
---

# Background Component Deployment


LeslieCleaner should minimize background processes.


Principle:


Do not create unnecessary System services.



---

# Background Running Mode


Preferred:


User-level background task



Avoid:


Permanent System Service


unless required.



---

# Background Task Usage


Suitable for:


Scheduled cleanup reminder


↓

Update checking


↓

Health monitoring



---

# Background Task Restrictions


Must:


Low CPU usage


↓

Low memory usage


↓

User controllable



---

# Background Task Settings


User can configure:


```

Enable background monitoring

□


Start with System

□


Run scheduled scan

□


```



---

# Startup Registration Deployment


LeslieCleaner startup behavior must be transparent.



---

# Startup Entry Rules


If enabled:


Create startup entry.



If disabled:


Remove startup entry.



---

# Startup Registration Location


Recommended:


System Startup Manager API


or


Registry:



```



---

# Startup Permission


Default:


Disabled.



User must manually enable.



---

# First Installation


Do not automatically add startup.



---

# Startup Optimization Relationship


LeslieCleaner itself should appear as:


User Application Startup



Not:


System Startup.



---

# Configuration Migration


Purpose:


Preserve user settings during upgrade.



---

# Migration Data


Include:


```

Application Settings


↓

Scan Preferences


↓

Whitelist


↓

Blacklist


↓

UI Preferences


↓

Scheduled Tasks


```



---

# Migration Process


```

Detect Old Version


↓

Read Configuration


↓

Convert Format


↓

Save New Format


↓

Verify


```



---

# Configuration Versioning


Example:


```
ConfigVersion = 1


```



Future:


```
ConfigVersion = 2


```



Migration required when:


Version changes.



---

# Database Migration


SQLite database requires:


Migration mechanism.



---

# Database Upgrade Process


```

Backup Database


↓

Execute Migration


↓

Verify Tables


↓

Update Version


```



---

# Migration Failure Handling


If failed:


Restore backup database.



---

# Backup System Deployment


LeslieCleaner must provide recovery capability.



---

# Backup Types


## Registry Backup


Before:


Registry modification.



---

## Cleanup Backup


Before:


Important file deletion.



---

## Configuration Backup


Before:


Upgrade.



---

# Backup Directory


Example:


```

ProgramData

 └── LeslieCleaner

       └── Backup


```



---

# Backup Naming


Format:


```

Backup_YYYYMMDD_HHMMSS


```



Example:


```

Backup_20260726_153000


```



---

# Backup Retention


User configurable.



Default:


Keep latest 10 backups.



---

# Restore Process


```

Select Backup


↓

Verify Backup


↓

Confirm Restore


↓

Execute


↓

Restart if needed


```



---

# Installation Failure Recovery


The installer must handle:


Insufficient space


↓

Permission failure


↓

File lock


↓

Interrupted installation



---

# Rollback Strategy


If installation fails:


Restore:


```

Previous Files


↓

Configuration


↓

Database


```



---

# File Lock Handling


If application running:


Show:


```

LeslieCleaner is running.

Please close it before continuing.


```



---

# Self Repair Mechanism


Optional feature.


Purpose:


Repair damaged installation.



---

# Repair Function


Can restore:


```

Missing Program Files


↓

Broken Configuration


↓

Missing Resources


```



---

# Repair Process


```

Detect Installation


↓

Compare Files


↓

Restore Missing Files


↓

Verify


```



---

# Local Update Mechanism


Because LeslieCleaner is personal software:


No public update server required.



---

# Update Sources


Support:


Local package


↓

Manual update


↓

Private storage



---

# Update Package Structure


Example:


```

Update.zip


|

├── Application Files

├── Version.json

└── Migration Scripts


```



---

# Update Process


```

Check Version


↓

Backup Current Version


↓

Close Application


↓

Replace Files


↓

Migration


↓

Restart


```



---

# Version File


Example:


```
{
 "Version":"1.0.0",
 "BuildDate":"2026-07-26"
}

```



---

# Deployment Build Process


Development:


```

Debug Build


↓

Testing


↓

Release Build


↓

Package


```



---

# Release Build Optimization


Enable:


```

Optimize Code


↓

Remove Debug Symbols


↓

Enable Compiler Optimization


```



---

# Build Output Structure


Example:


```

Build


|

├── LeslieCleaner.exe

├── Dependencies

├── Resources

└── Config


```



---

# Portable Mode (Optional)


Support optional portable operation.



---

# Portable Mode Rules


Store:


```

Database

↓

Config

↓

Logs


```


inside:


```
.\Data

```



---

# Portable Mode Restrictions


Do not:


Modify system automatically.



---

# Installer Security


Installer should verify:


File integrity


↓

Hash


↓

Source validity



---

# Deployment Cleanup


Before packaging:


Remove:


```

Debug Files


↓

Temporary Files


↓

Test Data


↓

Unused Resources


```



---

# Final Deployment Checklist


Before installation package creation:


□ Build successful


□ Release configuration


□ Dependencies checked


□ Installer tested


□ Custom path tested


□ Upgrade tested


□ Uninstall tested


□ Data preservation tested


□ Rollback tested



---

# Final Deployment Principle


LeslieCleaner installation must provide:


Professional installation experience


while remaining simple and suitable for personal long-term usage.



END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

