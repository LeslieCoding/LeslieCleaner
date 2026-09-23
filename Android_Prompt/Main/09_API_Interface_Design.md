# LeslieCleaner Android Development Bible - 09_API_Interface_Design

> Android Prompt ID: AP-MAIN-09-API-INTERFACE-DESIGN-MD  
> Source Prompt: `Main\09_API_Interface_Design.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-09-API-INTERFACE-DESIGN-MD` in the Android traceability matrix.
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

API Interface Design Specification

Priority

★★★★★

---

# Purpose

This document defines all internal API interfaces of LeslieCleaner.

The purpose:

Create a stable communication layer between modules.

Ensure:

Loose Coupling

↓

High Maintainability

↓

Easy Testing

↓

Future Expansion

↓

AI Code Generation Consistency


---

# API Architecture Principle


LeslieCleaner uses:


```

UI Layer


↓

Application Service Layer


↓

Domain Service Layer


↓

Infrastructure Layer


```


---

# Interface Design Rules


Every module must expose:


Service Interface


↓

DTO Models


↓

Operation Result


↓

Event Notification


---

# Forbidden Design


Avoid:


UI directly calling:

  API


↓

Registry API


↓

File System API


↓

Database API


---

# Unified API Response Model


All service methods return:


```
OperationResult<T>

{


Success


Message


ErrorCode


Data


Duration


Warnings


}


```


---

# OperationResult Definition


## Success


Boolean


Example:


```
true

```


---

## Message


User-readable message.


Example:


```
Cleanup completed successfully

```


---

## ErrorCode


System error identifier.


Example:


```
LC-CLEAN-001

```


---

## Data


Actual returned object.


---

## Duration


Execution time.


Used for:


Performance analysis.


---

## Warnings


Non-critical issues.


Example:


```
Some files were locked

```


---

# Progress Reporting Interface


Long operations must support:


```
IProgressReporter

```


---

# Progress Model


```
ProgressInfo

{

CurrentTask

Percentage

ProcessedCount

TotalCount

CurrentItem

EstimatedTime

}

```


---

# Cancellation Interface


Every long-running operation supports:


```
CancellationToken

```


---

# Module Service Standard


Every major module contains:


```
IXXXService


IXXXScanner


IXXXAnalyzer


IXXXExecutor


```


---

# Cleaner API Design


## Cleaner Service Interface


```
ICleanerService

{

ScanAsync()


AnalyzeAsync()


CleanAsync()


CancelAsync()


GetHistory()


}

```


---

# Scan API


Method:


```
ScanAsync()

```


Purpose:


Analyze removable junk.


---

Input:


```
ScanRequest

{

ScanType

Categories

DeepScan

}

```


---

ScanType:


```
Quick

Smart

Deep

```


---

Categories:


```
System

Browser

Cache

Privacy

Application

```


---

Output:


```
ScanResult

{

TotalItems

TotalSize

RiskLevel

Items[]

}

```


---

# Clean API


Method:


```
CleanAsync()

```


---

Input:


```
CleanRequest

{

SelectedItems

CreateBackup

Verify

}

```


---

Output:


```
CleanResult

{

DeletedCount

FreedSpace

FailedCount

}

```


---

# Registry API Design


## Registry Service Interface


```
IRegistryService

{


ScanRegistry()


BackupRegistry()


CleanRegistry()


RestoreRegistry()


}

```


---

# Registry Scan API


Method:


```
ScanRegistryAsync()

```


Input:


```
RegistryScanRequest

{

ScanLevel

Categories

}

```


---

ScanLevel:


```
Safe

Normal

Deep

```


---

Output:


```
RegistryScanResult

{

ProblemCount

RiskScore

Items[]

}

```


---

# Registry Clean API


Method:


```
CleanRegistryAsync()

```


Input:


```
RegistryCleanRequest

{

Items

BackupId

}

```


---

Output:


```
RegistryCleanResult

{

RemovedCount

BackupPath

RollbackAvailable

}

```


---

# Registry Backup API


Method:


```
CreateRegistryBackupAsync()

```


Output:


```
BackupResult

{

BackupId

Path

Size

}

```


---

# Registry Restore API


Method:


```
RestoreRegistryAsync()

```


Input:


```
RestoreRequest

{

BackupId

}

```


---

# Uninstaller API Design


## Main Interface


```
IUninstallerService

{


GetApplications()


AnalyzeApplication()


UninstallAsync()


ScanResidualAsync()


CleanResidualAsync()


}

```


---

# Application Discovery API


Method:


```
GetApplicationsAsync()

```


Output:


```
List<ApplicationInfo>

```


---

# ApplicationInfo DTO


```
{

ApplicationId

Name

Publisher

Version

InstallPath

Size

RiskLevel

}

```


---

# Application Analysis API


Method:


```
AnalyzeApplicationAsync()

```


Input:


```
ApplicationId

```


Output:


```
ApplicationAnalysisResult

{

Fingerprint

Components

Risk

EstimatedResidual

}

```


---

# Uninstall API


Method:


```
UninstallAsync()

```


Input:


```
UninstallRequest

{

ApplicationId

CreateBackup

SilentMode

CleanResidual

}

```


---

Output:


```
UninstallResult

{

Success

RemovedSize

ResidualCount

BackupId

}

```


---

# Residual Scan API


Method:


```
ScanResidualAsync()

```


Input:


```
ResidualScanRequest

{

ApplicationId

FingerprintId

}

```


---

Output:


```
ResidualResult

{

Files[]

RegistryItems[]

Services[]

Tasks[]

Confidence

}

```


---

# Residual Cleanup API


Method:


```
CleanResidualAsync()

```


Input:


```
ResidualCleanRequest

{

Items

Backup

}

```


---

Output:


```
ResidualCleanResult

{

RemovedCount

FreedSpace

}

```


---

# Performance Requirements


All APIs:


Must be async.


Must support cancellation.


Must report progress.


Must log execution time.


---

# API Naming Standard


Use:


Verb + Object


Examples:


```
ScanRegistry

CleanFiles

RemoveApplication

AnalyzeStartup


```


---

# API Versioning


Future:


```
v1

v2

v3

```


Example:


```
ICleanerServiceV1

```


---

# Error Handling Standard


Every API must handle:


Permission Error


↓

File Locked


↓

Access Denied


↓

Database Failure


↓

System API Failure


---

# Logging Requirement


Each API call records:


```
Module

Method

StartTime

EndTime

Result

Exception

```


---

# API Security Rules


Sensitive operations require:


Permission Check


↓

Backup Check


↓

Confirmation


---

# API Architecture Final Requirement


All LeslieCleaner functions must communicate through clearly defined interfaces.

No hidden dependencies.

No direct cross-module access.

Every feature must be:

Testable

↓

Replaceable

↓

Optimizable


---
---

# Startup Manager API Design

## Purpose

Provide safe   startup management.

Core functions:

Detect startup items

↓

Classify startup items

↓

Analyze impact

↓

Optimize boot process

↓

Restore changes


---

# Startup Service Interface


```
IStartupService

{

ScanStartupItemsAsync()

AnalyzeStartupAsync()

EnableStartupAsync()

DisableStartupAsync()

DelayStartupAsync()

RestoreStartupAsync()

}

```


---

# Scan Startup API


Method:


```
ScanStartupItemsAsync()

```


Purpose:

Collect all startup entries.


---

Input:


```
StartupScanRequest

{

IncludeMicrosoft

IncludeServices

IncludeTasks

IncludeDrivers

}

```


---

Output:


```
StartupScanResult

{

TotalCount

MicrosoftCount

UserApplicationCount

Items[]

}

```


---

# Startup Item DTO


```
StartupItemInfo

{

StartupId

Name

Publisher

Path

Type

Category

Enabled

Protected

ImpactLevel

RiskLevel

Recommendation

}

```


---

# Startup Classification API


Method:


```
ClassifyStartupItemAsync()

```


Purpose:


Determine:

Microsoft System

or

User Application


---

Input:


```
StartupItemInfo

```


---

Output:


```
StartupClassificationResult

{

Category

Confidence

Reason

}

```


---

# Classification Result


Category:


```
MicrosoftSystem

UserApplication

Unknown

```


---

# Startup Impact Analysis API


Method:


```
AnalyzeStartupImpactAsync()

```


---

Input:


```
StartupId

```


---

Output:


```
StartupImpactResult

{

CpuImpact

MemoryImpact

DiskImpact

NetworkImpact

OverallLevel

}

```


---

# Startup Optimization API


Method:


```
OptimizeStartupAsync()

```


---

Input:


```
StartupOptimizationRequest

{

StartupId

Action

CreateBackup

}

```


---

Action:


```
Enable

Disable

Delay

Remove

```


---

Output:


```
StartupOperationResult

{

Success

BackupId

PreviousState

CurrentState

}

```


---

# Startup Restore API


Method:


```
RestoreStartupAsync()

```


Input:


```
BackupId

```


Output:


```
RestoreResult

{

Success

RestoredItems

}

```


---

# File Analyzer API Design


## Purpose


Analyze disk usage and discover unnecessary storage consumption.


---

# File Analyzer Interface


```
IFileAnalyzerService

{

AnalyzeDiskAsync()

FindLargeFilesAsync()

FindDuplicateFilesAsync()

FindOldFilesAsync()

}

```


---

# Disk Analysis API


Method:


```
AnalyzeDiskAsync()

```


Output:


```
DiskAnalysisResult

{

TotalSize

UsedSize

FreeSpace

Categories[]

}

```


---

# Large File Detection API


Method:


```
FindLargeFilesAsync()

```


Input:


```
LargeFileRequest

{

MinimumSize

ScanPath

}

```


Output:


```
LargeFileResult

{

Files[]

TotalSize

}

```


---

# Duplicate File Detection API


Method:


```
FindDuplicateFilesAsync()

```


Process:


```
Size Grouping

↓

Partial Hash

↓

Full Hash

↓

Result

```


---

Output:


```
DuplicateResult

{

Groups[]

WastedSpace

}

```


---

# Duplicate Group DTO


```
DuplicateGroup

{

Hash

FileCount

TotalSize

Files[]

}

```


---

# File Shredder API Design


## Purpose


Securely destroy files.


---

# Shredder Interface


```
IShredderService

{

AnalyzeFileAsync()

ShredAsync()

VerifyAsync()

GetHistoryAsync()

}

```


---

# Shred Analysis API


Method:


```
AnalyzeFileAsync()

```


Output:


```
ShredAnalysisResult

{

FileCount

TotalSize

LockedFiles

Warning

}

```


---

# Shred Execute API


Method:


```
ShredAsync()

```


Input:


```
ShredRequest

{

Files

Algorithm

Verify

}

```


---

# Shred Algorithm


```
Quick

Standard

Advanced

```


---

# Shred Result


```
ShredResult

{

SuccessCount

FailedCount

DestroyedSize

Duration

}

```


---

# Privacy Cleaner API Design


## Purpose


Remove privacy traces safely.


---

# Privacy Service Interface


```
IPrivacyService

{

ScanPrivacyAsync()

AnalyzeRiskAsync()

CleanPrivacyAsync()

}

```


---

# Privacy Scan API


Method:


```
ScanPrivacyAsync()

```


Output:


```
PrivacyScanResult

{

TotalItems

RiskScore

Categories[]

}

```


---

# Privacy Item DTO


```
PrivacyItem

{

Category

Application

Path

Size

RiskLevel

Protected

}

```


---

# Privacy Clean API


Method:


```
CleanPrivacyAsync()

```


Input:


```
PrivacyCleanRequest

{

Items

CreateBackup

}

```


---

Output:


```
PrivacyCleanResult

{

RemovedItems

FreedSpace

}

```


---

# Browser Cleaner API Design


## Purpose


Clean browser traces.


---

# Browser Service Interface


```
IBrowserCleanerService

{

DetectBrowsersAsync()

ScanBrowserAsync()

CleanBrowserAsync()

}

```


---

# Browser Detection API


Method:


```
DetectBrowsersAsync()

```


Output:


```
BrowserInfo[]

```


---

# Browser Info DTO


```
BrowserInfo

{

Name

Version

InstallPath

Profiles[]

}

```


---

# Browser Scan API


Method:


```
ScanBrowserAsync()

```


Input:


```
BrowserScanRequest

{

Browser

Profile

Categories

}

```


---

Categories:


```
Cache

History

Cookies

Session

DownloadRecords

```


---

# Browser Clean API


Method:


```
CleanBrowserAsync()

```


Output:


```
BrowserCleanResult

{

RemovedSize

RemovedItems

ProtectedItems

}

```


---

# AI Assistant API Design


## Purpose


Provide intelligent recommendations.


---

# AI Service Interface


```
IAIAssistantService

{

AnalyzeSystemAsync()

GenerateRecommendationAsync()

ExplainItemAsync()

}

```


---

# System Analysis API


Method:


```
AnalyzeSystemAsync()

```


Output:


```
SystemAnalysisResult

{

PerformanceScore

Problems[]

Recommendations[]

}

```


---

# AI Recommendation DTO


```
AIRecommendation

{

Title

Description

Risk

Action

RequireConfirm

}

```


---

# AI Safety Rules


AI API cannot:


Execute destructive operation directly.


Must return:


Recommendation

↓

User Confirmation Required

↓

Execution API


---

# Plugin API Design


## Purpose


Support third-party extensions.


---

# Plugin Interface


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

# Plugin Manager API


```
IPluginManagerService

{

InstallPlugin()

EnablePlugin()

DisablePlugin()

RemovePlugin()

GetPlugins()

}

```


---

# Plugin Information DTO


```
PluginInfo

{

Id

Name

Version

Author

Permission

RiskLevel

Status

}

```


---

# Update API Design


## Purpose


Manage software updates safely.


---

# Update Service Interface


```
IUpdateService

{

CheckUpdateAsync()

DownloadUpdateAsync()

InstallUpdateAsync()

RollbackAsync()

}

```


---

# Update Check Result


```
UpdateInfo

{

CurrentVersion

LatestVersion

ReleaseNotes

DownloadSize

}

```


---

# Event API Design


## Purpose


Enable loose communication.


---

# Event Interface


```
IEventBus

{

Publish()

Subscribe()

Unsubscribe()

}

```


---

# Standard Events


```
ScanStartedEvent

ScanCompletedEvent

CleanupStartedEvent

CleanupCompletedEvent

ApplicationRemovedEvent

StartupChangedEvent

BackupCreatedEvent

```


---

# API Final Standard


All APIs must:


✓ Async

✓ Cancelable

✓ Observable

✓ Logged

✓ Secure

✓ Testable

✓ Versionable


END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

