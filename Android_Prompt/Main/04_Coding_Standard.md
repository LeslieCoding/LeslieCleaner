# LeslieCleaner Android Development Bible - 04_Coding_Standard

> Android Prompt ID: AP-MAIN-04-CODING-STANDARD-MD  
> Source Prompt: `Main\04_Coding_Standard.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-04-CODING-STANDARD-MD` in the Android traceability matrix.
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

Coding Standard

Priority

★★★★★

This document defines mandatory coding standards for the LeslieCleaner project.

All source code generated by developers or AI assistants must comply with this document.

Code quality is considered a core product feature.

---

# Coding Philosophy

LeslieCleaner follows these principles:

Readable Code

↓

Maintainable Code

↓

Predictable Code

↓

Testable Code

↓

Performant Code

↓

Secure Code

↓

Simple Code

Complexity must be justified.

Shorter code does not mean better code.

The best code is code that can be understood and safely modified years later.

---

# Language Standard

Primary Language:

C#

Framework:

.NET

Architecture:

Modern Desktop Application

Coding style:

Microsoft C# Coding Convention

Additional requirements:

Nullable Reference Types Enabled

Async/Await Preferred

Dependency Injection Required

SOLID Principles Required

Warnings Treated Seriously

---

# General Code Rules

Every class must have a clear responsibility.

A class should not:

Manage UI

↓

Access Database

↓

Call   API

↓

Execute Business Rules

↓

Handle Logging

↓

Manage Configuration

at the same time.

Responsibilities must be separated.

---

# Single Responsibility Principle

A class should have one reason to change.

Bad:

```
SystemManager
{
    CleanFiles();

    ModifyRegistry();

    UpdateUI();

    SaveDatabase();

    WriteLog();
}
```

Problems:

Too many responsibilities.

Difficult testing.

High coupling.

---

Good:

```
FileCleaner

RegistryCleaner

DashboardViewModel

HistoryRepository

LoggingService
```

Each component focuses on one responsibility.

---

# Naming Rules

Naming must communicate intention.

Names should explain:

What

Why

Purpose

Scope

Avoid meaningless names.

---

# Class Naming

Classes use:

PascalCase

Examples:

```
CleanerService

RegistryScanner

StartupAnalyzer

BackupManager

PluginLoader
```

---

Avoid:

```
Data

Helper

Manager

Handler

Processor

Common
```

unless the responsibility is truly clear.

---

# Interface Naming

Interfaces begin with:

I

Examples:

```
ICleanerService

IRegistryProvider

IBackupRepository

IScanner
```

Interfaces describe capability.

Not implementation.

---

# Implementation Naming

Implementation should describe behavior.

Example:

Interface:

```
IFileScanner
```

Implementation:

```
NtfsFileScanner

FastFileScanner

DeepFileScanner
```

Avoid:

```
FileScannerImpl
```

---

# Method Naming

Methods use:

PascalCase

Methods should describe actions.

Examples:

```
ScanFiles()

AnalyzeRegistry()

CreateBackup()

RestoreOperation()

CalculateRiskScore()
```

---

Avoid:

```
DoWork()

Process()

Handle()

ExecuteSomething()
```

unless context is extremely clear.

---

# Boolean Naming

Boolean variables must describe state.

Good:

```
isEnabled

hasBackup

canRestore

shouldDelete
```

Bad:

```
flag

status

check

value
```

---

# Constant Naming

Constants:

PascalCase

Examples:

```
MaximumScanThreads

DefaultCacheSize

RegistryBackupTimeout
```

Do not use:

```
MAX_SIZE

VALUE1

CONST_A
```

---

# Enum Naming

Enums use singular nouns.

Example:

```
public enum ScanStatus
{
    Pending,
    Running,
    Completed,
    Failed
}
```

Avoid:

```
ScanStatuses
```

---

# File Naming

File name must match main class name.

Example:

Class:

```
RegistryScanner
```

File:

```
RegistryScanner.cs
```

---

# Namespace Rules

Namespace follows directory structure.

Example:

Folder:

```
LeslieCleaner.Application.Cleaner.Scanner
```

Namespace:

```
namespace LeslieCleaner.Application.Cleaner.Scanner;
```

---

# Folder Naming

Folders use:

PascalCase

Examples:

```
Services

Repositories

ViewModels

Commands

Queries
```

Avoid:

```
service

helpers

misc

temp
```

---

# Method Length Rules

A method should usually be:

≤50 lines

If longer:

Analyze responsibility.

Extract methods.

Create services.

Separate workflows.

Long methods indicate missing abstractions.

---

# Class Length Rules

A class should usually be:

≤300 lines

If exceeded:

Split responsibilities.

Extract:

Services

Components

Strategies

Repositories

---

# Parameter Rules

Avoid methods with too many parameters.

Maximum recommended:

5 parameters

If more:

Create parameter object.

Example:

Bad:

```
CreateBackup(
path,
name,
size,
time,
user,
type,
mode
)
```

---

Better:

```
CreateBackup(BackupOptions options)
```

---

# Return Value Rules

Avoid returning primitive values when meaning exists.

Bad:

```
int result;
```

Better:

```
ScanResult result;
```

---

# Null Handling Rules

Nullable values must be explicit.

Avoid:

```
null everywhere
```

Use:

Nullable Reference Types

Result Pattern

Optional Types

Validation

---

# Defensive Programming

Validate external input.

Examples:

File Path

Registry Path

User Input

Plugin Data

Configuration

Network Data

Never trust external data.

---

# Comment Rules

Comments explain:

Why

not:

What

Bad:

```
// loop through files
foreach(file in files)
```

Good:

```
// Use batch processing here because large folders may contain millions of files.
```

---

# Code Documentation

Public APIs require XML documentation.

Example:

```
/// <summary>
/// Scans installed applications and generates inventory information.
/// </summary>
```

---

# TODO Rules

TODO comments require:

Owner

Reason

Expected Completion

Example:

```
// TODO(User): Replace temporary algorithm after benchmark validation.
```

Random TODO comments are prohibited.

---

# Magic Number Rules

Forbidden:

```
if(size > 1024)
```

Allowed:

```
if(size > MinimumLargeFileSize)
```

All meaningful values must become named constants.

---

# String Rules

User-facing text must never be hardcoded.

Bad:

```
MessageBox.Show("Cleaning completed");
```

Good:

```
Localization.Get("CleaningCompleted")
```

---

# Collection Rules

Prefer:

IEnumerable

↓

IReadOnlyCollection

↓

IReadOnlyList

when modification is unnecessary.

Expose the minimum required capability.

---
---

# Async Programming Rules

LeslieCleaner is a highly interactive desktop application.

Any operation that may take more than a few milliseconds must not block the UI thread.

Forbidden:

Synchronous File IO

↓

Synchronous Registry Scanning

↓

Synchronous Database Operations

↓

Long Calculations

↓

Network Requests

↓

Large File Processing

inside the UI thread.

---

# Async/Await Standard

All asynchronous operations must use:

async

await

Task

ValueTask (when appropriate)

Example:

Good:

```csharp
public async Task<ScanResult> ScanAsync(
    CancellationToken cancellationToken)
{
    return await scanner.ScanAsync(cancellationToken);
}
```

---

Avoid:

```csharp
public ScanResult Scan()
{
    Thread.Sleep(5000);

    return result;
}
```

---

# Async Naming Convention

Async methods must end with:

Async

Examples:

```
ScanAsync()

DeleteFileAsync()

CreateBackupAsync()

LoadConfigurationAsync()
```

---

# Async Exception Handling

Exceptions from async methods must be handled properly.

Never:

```
async void
```

except:

UI event handlers.

---

Correct:

```
async Task Button_Click()
```

---

# ConfigureAwait Rules

Library-level code should use:

```
ConfigureAwait(false)
```

when UI context is unnecessary.

Example:

```
await service.ExecuteAsync()
.ConfigureAwait(false);
```

---

# CancellationToken Rules

Every long-running operation must support cancellation.

Required operations:

File Scan

↓

Registry Scan

↓

Uninstall

↓

Backup

↓

Restore

↓

Database Migration

↓

AI Processing

↓

Update Download

---

Example:

```csharp
public async Task ScanAsync(
    CancellationToken cancellationToken)
{
    cancellationToken.ThrowIfCancellationRequested();

    await ProcessAsync();
}
```

---

# Cancellation Requirements

Every background task must support:

Start

↓

Pause

↓

Resume

↓

Cancel

↓

Cleanup

---

After cancellation:

Release resources.

Save state if required.

Write log.

Notify user.

---

# Task Management Rules

Never create unmanaged background tasks.

Forbidden:

```
Task.Run(() =>
{
    DoSomething();
});
```

without tracking.

---

All tasks must be registered:

Task Scheduler

↓

Task Manager

↓

Cancellation Source

↓

Lifecycle Management

---

# Threading Architecture Rules

Threads are divided into:

UI Thread

↓

Scan Threads

↓

Database Thread

↓

Logging Thread

↓

AI Thread

↓

Background Maintenance Thread

---

# UI Thread Rules

The UI thread may only handle:

Rendering

↓

Interaction

↓

Navigation

↓

State Update

---

The UI thread must never:

Scan files

↓

Modify registry

↓

Delete files

↓

Calculate hashes

↓

Execute database queries

---

# Thread Safety Rules

Shared data must be protected.

Allowed:

Immutable Objects

↓

Concurrent Collections

↓

Locks

↓

Channels

↓

Actors

---

Avoid:

Global Mutable State

Static Variables

Shared Lists

Unsafe Cache

---

# Lock Usage Rules

Locks should be:

Small Scope

↓

Short Duration

↓

Predictable

---

Forbidden:

Large code blocks inside locks.

Example:

Bad:

```csharp
lock(obj)
{
    ScanEntireDisk();
}
```

---

Better:

```csharp
var data = LoadData();

lock(obj)
{
    UpdateState(data);
}
```

---

# Deadlock Prevention

Avoid:

Nested Locks

↓

Circular Waiting

↓

Blocking Async

↓

.Wait()

↓

.Result

---

Forbidden:

```csharp
var result = task.Result;
```

---

Use:

```csharp
var result = await task;
```

---

# Parallel Processing Rules

Parallel processing should be used for:

Large File Scanning

↓

Hash Calculation

↓

Application Analysis

↓

Residual Detection

↓

Duplicate Search

---

Avoid uncontrolled parallelism.

Forbidden:

```
Parallel.ForEach(allFiles)
```

with millions of files.

---

# Concurrency Limits

Every parallel operation must define:

Maximum Threads

↓

Queue Size

↓

Timeout

↓

Cancellation

---

Example:

```
MaxScanThreads = CPU cores - 1
```

---

# Memory Optimization Rules

Memory efficiency is mandatory.

Avoid:

Large Object Allocation

↓

Repeated String Creation

↓

Unnecessary Copy

↓

Loading Entire Files

↓

Duplicate Collections

---

# Large File Processing Rules

Never:

```
File.ReadAllBytes()
```

for large files.

---

Use:

Streaming

↓

Buffered Reading

↓

Memory Mapping

---

Example:

```
FileStream

BufferedStream

MemoryMappedFile
```

---

# Collection Optimization

Choose correct collections.

Examples:

Dictionary

↓

Fast Key Lookup

HashSet

↓

Unique Items

Queue

↓

Task Processing

Stack

↓

Reverse Processing

List

↓

Ordered Data

---

# Object Lifecycle Rules

Every object should have clear ownership.

Questions:

Who creates it?

↓

Who uses it?

↓

Who disposes it?

↓

How long does it live?

---

# IDisposable Rules

Objects owning resources must implement IDisposable.

Examples:

FileStream

↓

Registry Handle

↓

Database Connection

↓

Timer

↓

Native Handle

---

Correct:

```csharp
using var stream =
    new FileStream(path);
```

---

# Resource Release Rules

Always release:

Files

↓

Streams

↓

Database Connections

↓

Registry Handles

↓

Threads

↓

Timers

↓

Native Resources

---

# Garbage Collection Rules

Do not manually force GC.

Forbidden:

```
GC.Collect()
```

unless proven necessary.

---

# String Optimization Rules

Avoid excessive concatenation.

Bad:

```
result += item;
```

for large loops.

---

Use:

```
StringBuilder
```

---

# File Operation Rules

All file operations must go through:

IFileService

---

Forbidden:

Direct usage:

```
File.Delete()

Directory.Delete()

File.Move()
```

inside business modules.

---

# File Safety Rules

Before deleting:

Check existence

↓

Check permission

↓

Create backup

↓

Record metadata

↓

Execute

↓

Verify

---

# Registry Operation Rules

All registry operations must go through:

IRegistryService

---

Forbidden:

Direct usage:

```
Registry.CurrentUser
Registry.LocalMachine
```

inside business modules.

---

# Database Access Rules

Database access path:

Service

↓

Repository

↓

Database Provider

---

Forbidden:

SQL inside ViewModel.

SQL inside Controller.

SQL inside Domain.

---

# Transaction Rules

Database write operations must use transactions.

Example:

Backup Record

↓

Operation Record

↓

History Record

↓

Commit

---

# Exception Safety Rules

Every external operation requires:

Try

↓

Catch

↓

Log

↓

Recover

↓

Notify

---

Never silently ignore exceptions.

Forbidden:

```
catch(Exception)
{
}
```

---

# Error Handling Rules

Errors must be classified:

User Error

↓

Permission Error

↓

System Error

↓

Network Error

↓

Internal Error

---

Each error should provide:

Message

Cause

Solution

Recovery Option

---
---

# Logging Coding Standard

Logging is mandatory for all important operations.

No core module may operate without logging.

All modules must use:

ILogger

or

ILeslieLogger

---

# Log Levels

Supported levels:

Trace

↓

Debug

↓

Information

↓

Warning

↓

Error

↓

Critical

---

# Log Level Usage

Trace:

Detailed internal execution information.

Example:

Method entering.

Variable state.

Detailed workflow.

Only for development.

---

Debug:

Debugging information.

Example:

Rule matching process.

Scanner decision.

Cache status.

---

Information:

Normal successful operations.

Example:

Scan completed.

Backup created.

Cleanup finished.

---

Warning:

Potential problems.

Example:

Permission denied.

Unknown registry item.

Low confidence recommendation.

---

Error:

Operation failure.

Example:

File deletion failed.

Database exception.

Plugin crash.

---

Critical:

Application-level failure.

Example:

Database corruption.

Startup failure.

Security issue.

---

# Structured Logging

Logs must be structured.

Bad:

```
"Clean failed"
```

Good:

```
{
 Module:"Cleaner",
 Operation:"DeleteCache",
 FileCount:1250,
 ErrorCode:"ACCESS_DENIED"
}
```

---

# Required Log Information

Important logs should include:

Timestamp

↓

Module

↓

Operation

↓

User Action

↓

Execution Duration

↓

Result

↓

Exception

↓

Machine Information

↓

Version

---

# Performance Logging

Long-running operations must record:

Start Time

↓

End Time

↓

Duration

↓

CPU Usage

↓

Memory Usage

↓

IO Usage

---

Example:

```
Scan completed.

Files:
120000

Duration:
15.4 seconds

Memory:
220MB
```

---

# Security Coding Standard

Security is mandatory.

Every external input must be treated as unsafe.

Sources:

User Input

↓

File Path

↓

Registry Data

↓

Plugin Data

↓

Network Data

↓

Configuration

---

# Path Validation Rules

Before file operations:

Normalize Path

↓

Validate Location

↓

Check Permission

↓

Check Symbolic Link

↓

Check File Type

↓

Execute

---

Prevent:

Path Traversal

Directory Escape

Unauthorized Access

---

# File Deletion Security

Before deleting:

Verify Target

↓

Generate Backup

↓

Confirm User Permission

↓

Execute

↓

Verify Removal

↓

Record History

---

# Registry Security

Before registry modification:

Backup Key

↓

Validate Ownership

↓

Check Risk Level

↓

Execute

↓

Verify

↓

Rollback Available

---

# Privilege Rules

Administrator privilege must not be requested unnecessarily.

Allowed:

Registry system modification

↓

Service management

↓

Driver operations

↓

Protected file cleanup

---

Forbidden:

Request administrator permission at startup.

---

# Sensitive Data Rules

The application must protect:

User Documents

↓

Browser Passwords

↓

Cookies

↓

Personal Files

↓

Credentials

---

Never collect or transmit without explicit permission.

---

# Dependency Injection Standard

All services must use Dependency Injection.

Forbidden:

```
new Service()
```

inside business logic.

---

Correct:

```
constructor(IService service)
{
}
```

---

# Service Lifetime Rules

Use appropriate lifetime.

Singleton:

Stateless shared services.

Examples:

ConfigurationService

LoggingService


---

Scoped:

Operation-level services.

Examples:

ScanContext


---

Transient:

Short-lived objects.

Examples:

CommandHandler

Validator

---

# Service Design Rules

Services should:

Provide one capability.

Have clear interfaces.

Be testable.

Avoid static state.

Avoid global variables.

---

# Repository Standard

Repositories abstract data access.

Example:

Interface:

```
IHistoryRepository
```

Implementation:

```
HistoryRepository
```

---

Repositories are responsible for:

Query

↓

Insert

↓

Update

↓

Delete

↓

Transaction

---

Repositories are not responsible for:

Business Rules

↓

UI

↓

Logging Decision

↓

User Notification

---

# Domain Model Standard

Entities contain:

Identity

↓

State

↓

Business Behavior

---

Avoid:

Anemic Models.

Example:

Bad:

```
User
{
Name;
Age;
}
```

No behavior.

---

Better:

```
BackupTask
{
Create();

Validate();

Cancel();

Restore();
}
```

---

# DTO Standard

DTOs are used for:

Data Transfer

↓

API Communication

↓

UI Binding

↓

Module Communication

---

DTOs should not contain business logic.

---

# Result Pattern Standard

Operations should return explicit results.

Avoid:

```
bool success
```

---

Use:

```
OperationResult
{
Success

Message

ErrorCode

Data

}
```

---

# MVVM Coding Standard

The UI layer follows strict MVVM.

Components:

View

↓

ViewModel

↓

Service

↓

Domain

---

# View Rules

Views contain:

Layout

↓

Binding

↓

Visual State

↓

Animation

---

Views must not contain:

Business Logic

↓

Database Access

↓

File Operations

↓

Registry Operations

---

# ViewModel Rules

ViewModel responsibilities:

Expose State

↓

Handle Commands

↓

Call Services

↓

Update UI State

---

ViewModels must not:

Directly access   API.

Directly access Database.

Perform heavy computation.

---

# Command Rules

User operations must use Commands.

Examples:

StartScanCommand

CleanCommand

DeleteCommand

RestoreCommand

---

Avoid:

Button Click Event containing logic.

---

# Observable State Rules

UI state should use:

ObservableObject

↓

INotifyPropertyChanged

↓

Reactive Pattern

---

Avoid:

Manual UI refresh calls.

---

# UI Thread Update Rules

Background tasks updating UI must use dispatcher.

Example:

```
Dispatcher.Invoke()
```

or

appropriate UI framework mechanism.

---

# UI Error Handling

UI should display:

Friendly Message

↓

Reason

↓

Solution

↓

Recovery Action

---

Never show:

Raw Exception

Stack Trace

Internal Path

---

# Unit Test Coding Standard

Every important class requires tests.

Priority:

Domain Logic

↓

Services

↓

Repositories

↓

Converters

↓

ViewModels

---

# Test Naming Convention

Format:

Method_State_ExpectedResult

Example:

```
Scan_WhenFolderEmpty_ReturnsNoItems()
```

---

# Test Structure

Use:

Arrange

↓

Act

↓

Assert

---

Example:

```
Arrange test data.

Execute operation.

Verify result.
```

---

# Test Quality Rules

Tests must verify:

Normal Case

↓

Boundary Case

↓

Exception Case

↓

Performance Case

---

# Mock Rules

Mock:

External Dependencies

↓

File System

↓

Database

↓

  API

↓

Network

---

Do not mock simple business objects unnecessarily.

---
---

# AI Generated Code Standard

All AI-generated code must follow the same standards as human-written code.

AI is not allowed to generate:

Temporary solutions

↓

Prototype code

↓

Pseudo implementation

↓

Incomplete methods

↓

Placeholder returns

↓

Fake data

↓

Unused abstractions

---

# AI Code Requirements

Before generating code, AI must analyze:

Requirement

↓

Architecture

↓

Existing Code

↓

Dependencies

↓

Performance Impact

↓

Security Impact

↓

Testing Strategy

---

# AI Implementation Rules

AI must:

Reuse existing architecture.

Reuse existing services.

Reuse existing interfaces.

Follow naming conventions.

Add necessary tests.

Add documentation.

Handle exceptions.

Consider memory usage.

Consider concurrency safety.

---

# AI Forbidden Code Patterns

Forbidden:

```csharp
throw new NotImplementedException();
```

unless explicitly requested.

---

Forbidden:

```csharp
// TODO implement later
```

without a documented reason.

---

Forbidden:

```csharp
return null;
```

to bypass logic.

---

Forbidden:

```
catch(Exception)
{
}
```

---

Forbidden:

Static global state.

---

Forbidden:

Huge classes.

---

Forbidden:

Huge methods.

---

Forbidden:

Duplicate implementations.

---

# Code Review Checklist

Every Pull Request must review:

## Architecture

☐ Correct layer

☐ Correct dependency direction

☐ No circular dependency

☐ Proper abstraction


---

## Code Quality

☐ Naming correct

☐ Formatting correct

☐ No duplicated code

☐ No unnecessary complexity

☐ Comments meaningful


---

## Performance

☐ No UI blocking

☐ Async used correctly

☐ Memory impact checked

☐ Large data handled efficiently

☐ Resource released


---

## Security

☐ Input validated

☐ Permission checked

☐ Sensitive data protected

☐ Dangerous operation confirmed


---

## Testing

☐ Unit tests added

☐ Edge cases covered

☐ Failure cases tested

☐ Regression tested


---

# Performance Coding Standard

Performance is a design requirement.

Developers must consider:

CPU

↓

Memory

↓

Disk IO

↓

Thread Usage

↓

Database

↓

UI Rendering

---

# CPU Optimization Rules

Avoid:

Unnecessary loops

↓

Repeated calculations

↓

Blocking operations

↓

Busy waiting

---

Prefer:

Caching

↓

Parallel processing

↓

Incremental calculation

↓

Early exit

---

# Disk IO Optimization

Avoid:

Repeated file opening

↓

Repeated directory traversal

↓

Full disk scanning

---

Prefer:

Indexing

↓

USN Journal

↓

Caching

↓

Batch operations

↓

Streaming

---

# Database Performance Rules

Required:

Prepared Statements

↓

Indexes

↓

Transactions

↓

Pagination

↓

Async Queries

---

Forbidden:

Loading huge tables into memory.

---

Example:

Bad:

```
GetAllHistory()
```

for millions of records.

---

Better:

```
GetHistoryPage(page,size)
```

---

# UI Performance Rules

UI must remain responsive.

Requirements:

60 FPS target

↓

Virtualized Lists

↓

Lazy Loading

↓

Async Loading

↓

Incremental Rendering

---

Avoid:

Rendering thousands of controls.

---

# Collection Performance Rules

For large datasets:

Use:

Virtualization

↓

Paging

↓

Streaming

---

Avoid:

Creating huge observable collections unnecessarily.

---

# Resource Management Rules

Every resource must have:

Owner

↓

Lifetime

↓

Cleanup Method

---

Resources include:

Files

↓

Streams

↓

Timers

↓

Threads

↓

Memory Buffers

↓

Native Handles

---

# High DPI Rules

UI must support:

100%

↓

125%

↓

150%

↓

200%

Scaling

---

Never use:

Fixed pixel layouts everywhere.

---

# Responsive UI Rules

Every page must support:

Loading

↓

Empty

↓

Success

↓

Warning

↓

Error

↓

Offline

---

No blank screens.

---

# UI Consistency Rules

All pages must use:

Same spacing

↓

Same typography

↓

Same buttons

↓

Same dialogs

↓

Same notifications

---

Do not create one-off designs.

---

# Error Recovery Coding Rules

Every critical operation requires recovery.

Examples:

Cleaning

↓

Uninstall

↓

Registry Modification

↓

File Shredding

↓

Update

---

Recovery workflow:

Detect Failure

↓

Restore State

↓

Record Error

↓

Notify User

↓

Provide Solution

---

# Release Code Quality Rules

Before release:

Remove:

Debug Code

↓

Test Data

↓

Temporary Files

↓

Unused Dependencies

↓

Unused Resources

↓

Experimental Features

---

Verify:

Build

↓

Tests

↓

Performance

↓

Memory

↓

Security

↓

Installer

---

# Code Cleanup Rules

After development:

Analyze:

Unused Classes

↓

Unused Methods

↓

Unused Packages

↓

Duplicate Code

↓

Dead Code

---

Remove unnecessary files.

Maintain minimal project size.

---

# Refactoring Rules

Refactoring should improve:

Readability

↓

Performance

↓

Maintainability

↓

Testability

---

Never refactor only for appearance.

---

# Technical Debt Rules

Technical debt must be recorded.

Each item contains:

Description

↓

Reason

↓

Impact

↓

Priority

↓

Solution Plan

---

# Final Coding Standard Acceptance

Code is accepted only when:

✓ Architecture compliant

✓ Naming compliant

✓ Exception handled

✓ Logging added

✓ Tests completed

✓ Performance considered

✓ Memory considered

✓ Security reviewed

✓ Documentation updated

✓ AI rules followed

✓ Code review passed

---

# Final Statement

This document defines the mandatory coding standards for LeslieCleaner.

All developers, contributors and AI assistants must follow these rules.

Code quality directly determines:

Software Stability

↓

Performance

↓

Security

↓

Maintainability

↓

User Experience

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

