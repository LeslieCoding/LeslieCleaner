# LeslieCleaner Android Development Bible - 10_Code_Architecture_Standard

> Android Prompt ID: AP-MAIN-10-CODE-ARCHITECTURE-STANDARD-MD  
> Source Prompt: `Main\10_Code_Architecture_Standard.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-10-CODE-ARCHITECTURE-STANDARD-MD` in the Android traceability matrix.
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

Code Architecture Standard

Priority

★★★★★


---

# Purpose

This document defines the complete software engineering standards for LeslieCleaner.

It controls:

Project Structure

↓

Code Organization

↓

Architecture Pattern

↓

Class Design

↓

Dependency Management

↓

Coding Style

↓

Testing Standard

↓

AI Code Generation Rules


All generated code must comply with this document.

---

# Core Development Philosophy


LeslieCleaner must be:


Maintainable

↓

Readable

↓

Extensible

↓

Testable

↓

Performance Optimized

↓

Secure


---

# Recommended Technology Architecture


Desktop Application:


Recommended:


```
.NET 8

+

C#

+

WPF

+

MVVM

+

SQLite

+

  API

```


---

# Architecture Pattern


Use:


## MVVM + Clean Architecture


Architecture layers:


```

Presentation Layer


↓

Application Layer


↓

Domain Layer


↓

Infrastructure Layer


↓

Platform Layer


```


---

# Layer Responsibilities


## Presentation Layer


Responsible:


UI

↓

View

↓

ViewModel

↓

User Interaction


---

Forbidden:


Business Logic


↓

Database Access


↓

  API


---

# Application Layer


Responsible:


Application workflows.


Examples:


Uninstall Application


↓

Execute Cleanup


↓

Analyze Startup


---

Contains:


Application Services


↓

DTO


↓

Commands


↓

Queries


---

# Domain Layer


Responsible:


Core business rules.


Examples:


Cleaning Rules


↓

Risk Evaluation


↓

Fingerprint Matching


---

Should not depend on:


UI

↓

Database

↓

  API


---

# Infrastructure Layer


Responsible:


External implementation.


Examples:


SQLite


↓

File System


↓

Registry


↓

Network


---

# Platform Layer


Responsible:


  specific functions.


Examples:


  Registry API


↓

Service Manager API


↓

Scheduled Task API


↓

  Event API


---

# Project Structure Standard


Solution:


```
LeslieCleaner.sln

```


---

# Main Projects


```
LeslieCleaner.UI


LeslieCleaner.Application


LeslieCleaner.Domain


LeslieCleaner.Infrastructure


LeslieCleaner.Platform


LeslieCleaner.Tests


```


---

# UI Project Structure


```
LeslieCleaner.UI

│

├── Views

├── ViewModels

├── Controls

├── Resources

├── Styles

├── Converters

├── Themes


```


---

# Views


Contains:


Window


↓

Page


↓

UserControl


---

Example:


```
CleanerView.xaml

RegistryView.xaml

StartupView.xaml

```


---

# ViewModels


Each View requires:


Corresponding ViewModel.


Example:


```
CleanerViewModel.cs

RegistryViewModel.cs

StartupViewModel.cs

```


---

# Controls


Reusable components.


Examples:


```
ProgressCard

StatusCard

ScanResultList

ModernButton

```


---

# Resources


Contains:


Images


↓

Icons


↓

Localization


↓

Fonts


---

# Styles


Contains:


```
ButtonStyles

CardStyles

TextStyles

AnimationStyles

```


---

# Application Project Structure


```
LeslieCleaner.Application


├── Services

├── Interfaces

├── DTOs

├── Commands

├── Queries

├── Validators


```


---

# Services Folder


Contains:


Application service implementations.


Example:


```
CleanerService.cs

UninstallerService.cs

StartupService.cs


```


---

# Interfaces Folder


Contains:


Contracts.


Example:


```
ICleanerService.cs

IStartupService.cs

IRegistryService.cs


```


---

# DTO Folder


Contains:


Data Transfer Objects.


Example:


```
ApplicationInfoDto.cs

ScanResultDto.cs

CleanupResultDto.cs


```


---

# Domain Project Structure


```
LeslieCleaner.Domain


├── Entities

├── ValueObjects

├── Rules

├── Algorithms

├── Exceptions


```


---

# Entities


Core objects.


Example:


```
Application

StartupItem

CleanRule

Fingerprint


```


---

# ValueObjects


Objects without identity.


Example:


```
FileSize

RiskScore

Version


```


---

# Rules


Business rules.


Example:


```
RegistryRiskRule

CleanupSafetyRule


```


---

# Algorithms


Core algorithms.


Example:


```
FingerprintMatcher

RiskCalculator

DuplicateDetector


```


---

# Infrastructure Project Structure


```
LeslieCleaner.Infrastructure


├── Database

├── Repositories

├── FileSystem

├── Logging

├── Configuration


```


---

# Database Folder


Contains:


```
SQLiteContext

Migration

DatabaseInitializer


```


---

# Repository Pattern


Example:


Interface:


```
IApplicationRepository


```


Implementation:


```
ApplicationRepository


```


---

# FileSystem Folder


Contains:


```
FileScanner

FileAnalyzer

FileOperator


```


---

# Logging Folder


Contains:


```
Logger

LogWriter

LogFormatter


```


---

# Platform Project Structure


```
LeslieCleaner.Platform


├── Registry

├──  Services

├── Startup

├── Process

├── Security


```


---

# Registry Folder


Contains:


```
RegistryScanner

RegistryBackup

RegistryOperator


```


---

#  Services Folder


Contains:


```
ServiceScanner

ServiceManager


```


---

# Startup Folder


Contains:


```
StartupScanner

StartupClassifier

StartupOptimizer


```


---

# Dependency Rule


Allowed:


```
UI

 ↓

Application

 ↓

Domain


Infrastructure

 ↓

Domain


Platform

 ↓

Domain


```


---

Forbidden:


```
Domain

↓

Infrastructure


```


---

# Dependency Injection Standard


Use:


Microsoft.Extensions.DependencyInjection


---

# Service Registration


Example:


```
services.AddSingleton<ICleanerService,
CleanerService>();

```


---

# Lifetime Rules


Singleton:


For:


Database

↓

Configuration

↓

Logger


---

Scoped:


For:


Operation Context


---

Transient:


For:


Temporary Services


---

# Class Design Principles


Every class should:


Have one responsibility.


---

Avoid:


God Class


Example:


```
SystemManager.cs

```


containing:


Cleaner

↓

Registry

↓

Startup

↓

Uninstaller


---

# Prefer:


```
CleanerService

RegistryService

StartupService

```


---

# Naming Standard


Classes:


PascalCase


Example:


```
CleanScanner


```


---

Methods:


PascalCase


Example:


```
ScanFilesAsync()


```


---

Variables:


camelCase


Example:


```
scanResult


```


---

Constants:


UPPER_CASE


Example:


```
MAX_SCAN_THREADS


```


---
---

# Async Programming Standard

## Purpose

LeslieCleaner contains many time-consuming operations:

File scanning

↓

Registry analysis

↓

Application detection

↓

Residual search

↓

Hash calculation

↓

Database processing


All long operations must be asynchronous.


---

# Mandatory Async Rule


Forbidden:


```
void ScanFiles()

{

// Long operation

}

```


---

Required:


```
Task<ScanResult> ScanFilesAsync()

{

}

```


---

# Async Naming Standard


All asynchronous methods must end with:


```
Async

```


Example:


```
CleanAsync()

AnalyzeAsync()

ScanAsync()

```


---

# UI Thread Protection


Never block UI thread.


Forbidden:


```
Task.Wait()

Result


```


---

Use:


```
await

```


---

# Cancellation Support


All long operations must support:


```
CancellationToken

```


Example:


```
Task ScanAsync(
CancellationToken cancellationToken)

```


---

# Progress Reporting


Long operations must report progress.


Use:


```
IProgress<T>

```


Example:


```
IProgress<ScanProgress>

```


---

# Operation State Model


Every long task has:


```
Created

↓

Running

↓

Paused

↓

Completed

↓

Cancelled

↓

Failed


```


---

# Multi-threading Architecture


LeslieCleaner uses:


Task Parallel Library


+

Controlled Thread Pool


---

# Thread Usage Rules


Never create unlimited threads.


Forbidden:


```
new Thread()

inside loop


```


---

Use:


```
Task.Run()

↓

Parallel

↓

SemaphoreSlim


```


---

# Scan Parallel Architecture


Example:


```
File Scanner


        ↓


Directory Queue


        ↓


Worker Tasks


        ↓


Result Collector


```


---

# Concurrency Control


Every parallel operation requires:


```
SemaphoreSlim

```


Example:


Maximum:


```
CPU cores × 2


```


---

# CPU Optimization


Detect:


```
Environment.ProcessorCount


```


Adjust:


```
Worker Count


```


---

# IO Optimization


Disk operations are limited by:


Disk Type


↓

System Load


↓

Current Usage


---

# SSD Strategy


Use:


More parallel reading


↓

Higher concurrency


---

# HDD Strategy


Use:


Sequential scanning


↓

Lower concurrency


---

# Memory Optimization Coding Rules


## Principle


Avoid unnecessary allocation.


---

# Forbidden


Large temporary collections.


Example:


```
List<FileInfo>

10 million items


```


---

# Recommended


Streaming:


```
IEnumerable<FileInfo>


```


or:


```
IAsyncEnumerable<FileInfo>


```


---

# Object Lifetime Management


Large objects:


Must release quickly.


Example:


```
using()

{

LargeResource

}


```


---

# IDisposable Standard


Classes holding:


File Handles


↓

Database Connections


↓

Streams


↓

Native Resources


Must implement:


```
IDisposable


```


---

# Memory Leak Prevention


Monitor:


```
Object Count

↓

GC Memory

↓

Handle Count

↓

Thread Count


```


---

# Cache Design Standard


Every cache requires:


Maximum Size


↓

Expiration Time


↓

Cleanup Strategy


---

# Example:


```
ScanCache


Max:

200MB


Expire:

7 days


```


---

# File Processing Standard


## File Scanner


Must support:


Large Directory


↓

Permission Error


↓

Locked File


↓

Symbolic Link


---

# File Access Rules


Before accessing:


Check:


Existence


↓

Permission


↓

File Status


---

# Large File Processing


Never:


Load entire file.


Forbidden:


```
byte[] data =
File.ReadAllBytes(path)


```


---

Use:


```
Stream


BufferedStream


```


---

# Hash Calculation Standard


Used for:


Duplicate Detection


↓

Fingerprint


↓

Verification


---

# Hash Strategy


Small File:


Full Hash


---

Large File:


Partial Hash


↓

Full Hash if required


---

# File Deletion Safety


Before delete:


Validate:


```
Path

Owner

FileType

Risk


```


---

# Protected Path List


Never delete automatically:


```
 

System32

Program Files

Drivers

Boot


```


---

# Registry API Standard


Registry operations must use:


Dedicated service.


---

Forbidden:


UI:


↓

Registry API


---

Correct:


```
UI

↓

RegistryService

↓

RegistryProvider


```


---

# Registry Modification Rules


Before modification:


1.

Create Backup


2.

Validate Key


3.

Check Permission


4.

Execute


5.

Verify


---

#   API Wrapper Standard


Never call native API everywhere.


Create:


```
 ApiWrapper


```


---

Example:


```
I ProcessService


I RegistryService


I ServiceManager


```


---

# Exception Handling Architecture


## Principle


Never silently fail.


---

# Exception Layers


```

Platform Exception


↓

Infrastructure Exception


↓

Application Exception


↓

UI Notification


```


---

# Custom Exception Types


Create:


```
LeslieException


DatabaseException


FileAccessException


RegistryException


PermissionException


```


---

# Exception Logging


Every exception records:


```
Module

Method

Message

StackTrace

Time


```


---

# User Error Message


Do not show:


Technical stack trace.


---

Bad:


```
NullReferenceException

0x00023


```


---

Good:


```
Unable to clean this file.

The file is currently in use.


```


---

# Logging System Standard


Recommended:


Serilog


---

# Log Levels


```
Trace

Debug

Information

Warning

Error

Critical


```


---

# Logging Categories


```
Application

Cleaner

Registry

Uninstaller

Startup

Database

Security


```


---

# Log File Structure


```
Logs

|

├── Application.log

├── Error.log

├── Operation.log


```


---

# Log Rotation


Rules:


Maximum Size


↓

Archive


↓

Delete Old Logs


---

# Sensitive Data Protection


Never log:


Passwords


↓

Cookies


↓

Personal Files Content


↓

Registry Values containing secrets


---

# Configuration Management


Use:


```
IConfigurationService


```


---

# Configuration Rules


Support:


Read


↓

Update


↓

Validate


↓

Save


---

# Configuration Storage


Recommended:


JSON


+

Encrypted Sensitive Values


---

# Unit Testing Standard


Every important module requires tests.


---

# Test Projects


```
LeslieCleaner.Tests


├── DomainTests

├── ServiceTests

├── IntegrationTests


```


---

# Unit Test Requirements


Test:


Normal Case


↓

Boundary Case


↓

Error Case


↓

Performance Case


---

# Required Test Coverage


Core modules:


Minimum:


80%


---

# Modules Requiring High Coverage


Cleaner


↓

Registry


↓

Uninstaller


↓

Startup


↓

Backup


---

# Code Review Rules


Before merging:


Check:


Architecture


↓

Security


↓

Performance


↓

Memory


↓

Naming


---

# AI Code Generation Rules


When AI generates code:


Must follow:


1.

MVVM architecture


2.

Dependency Injection


3.

Async programming


4.

Interface first design


5.

No duplicated code


6.

No hardcoded paths


7.

No UI business logic


8.

Complete exception handling


9.

Complete logging


10.

Complete comments for complex algorithms


---

# AI Forbidden Code


AI must not generate:


```
static global state


↓

Thread.Sleep


↓

Blocking IO


↓

Direct database access in UI


↓

Hardcoded   paths


↓

Unsafe delete operations


```


---

# Final Code Architecture Requirement


All LeslieCleaner source code must be:


Professional

↓

Production Ready

↓

Maintainable

↓

Performance Optimized

↓

Secure


The generated code must be suitable for commercial software release.


END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

