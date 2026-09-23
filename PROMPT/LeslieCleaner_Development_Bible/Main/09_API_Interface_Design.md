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