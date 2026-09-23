# LeslieCleaner Android Development Bible - 02_Architecture

> Android Prompt ID: AP-MAIN-02-ARCHITECTURE-MD  
> Source Prompt: `Main\02_Architecture.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-02-ARCHITECTURE-MD` in the Android traceability matrix.
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

System Architecture

Priority

★★★★★

This document defines the complete software architecture of LeslieCleaner.

All modules, services, engines and future extensions must comply with this architecture.

No implementation may violate the architecture defined in this document.

This document is subordinate only to:

00_Master_System_Prompt.md

01_Project_Rules.md

---

# Architecture Vision

LeslieCleaner is designed as a long-term enterprise desktop platform.

The architecture must satisfy:

High Performance

High Stability

High Maintainability

High Extensibility

High Testability

High Security

Low Coupling

High Cohesion

Future-proof

Plugin Friendly

The architecture must continue supporting new features after 5~10 years of development.

No redesign should be required because of today's implementation choices.

---

# Architecture Philosophy

Everything is a Module.

Everything communicates through Contracts.

Everything is Replaceable.

Everything is Observable.

Everything is Testable.

Everything is Configurable.

Business rules should never depend on implementation details.

Implementations may change.

Architecture should remain stable.

---

# Overall Architecture

LeslieCleaner follows a layered architecture.

Presentation Layer

↓

Application Layer

↓

Domain Layer

↓

Infrastructure Layer

↓

Platform Layer

Each layer has only one responsibility.

Cross-layer access is prohibited.

Only downward dependency is allowed.

Reverse dependency is forbidden.

---

# Layer Responsibilities

Presentation Layer

Responsible for:

Views

ViewModels

Navigation

Binding

Theme

Animation

Interaction

Localization

Accessibility

No business logic.

No file access.

No registry access.

No database access.

---

Application Layer

Responsible for:

Use Cases

Commands

Queries

Workflow

Business Coordination

Permission Validation

Transaction Control

Event Dispatch

This layer coordinates all modules.

---

Domain Layer

Responsible for:

Business Rules

Entities

Value Objects

Aggregates

Domain Events

Business Policies

Specifications

No UI.

No Database.

No  API.

No Infrastructure dependency.

---

Infrastructure Layer

Responsible for:

SQLite

Registry

File System

Network

Logging

Cache

Configuration

Serialization

 API

Everything related to external systems.

---

Platform Layer

Responsible for:

 Native API

COM

Shell

USN Journal

Task Scheduler

Service Controller

WMI

ETW

Performance Counter

Native File System

Kernel Interaction (when necessary)

This layer isolates -specific implementation.

---

# Architectural Principles

The architecture follows:

SOLID

DDD

CQRS (where appropriate)

Dependency Injection

Repository Pattern

Factory Pattern

Strategy Pattern

Mediator Pattern

Observer Pattern

Specification Pattern

Pipeline Pattern

Decorator Pattern

Composite Pattern

Only introduce patterns when they genuinely improve maintainability.

Avoid unnecessary abstraction.

---

# High-Level Modules

The system consists of the following major modules.

Dashboard

Cleaner

Registry

Uninstaller

Residual Scanner

Browser Cleaner

Startup Manager

Privacy Guard

Duplicate Finder

Large File Analyzer

File Shredder

Driver Manager

Service Manager

Extension Manager

Disk Analyzer

History Center

Notification Center

Settings Center

AI Assistant

Plugin Center

Update Center

Rule Engine

Task Scheduler

Telemetry (Optional)

Benchmark

Every module is independently deployable inside the architecture.

---

# Core Engines

LeslieCleaner is built around several shared engines.

Scan Engine

↓

Rule Engine

↓

Analysis Engine

↓

Recommendation Engine

↓

Execution Engine

↓

Rollback Engine

↓

History Engine

↓

Notification Engine

↓

Logging Engine

↓

Configuration Engine

↓

Plugin Engine

These engines are shared across every module.

No module should implement its own duplicate engine.

---

# Scan Engine

The Scan Engine is responsible for discovering:

Files

Folders

Registry

Services

Drivers

Scheduled Tasks

Startup Entries

Extensions

Browser Data

Cache

Logs

Temporary Files

Residual Files

The Scan Engine never deletes data.

Its only responsibility is discovery.

Results are passed to the Analysis Engine.

---

# Analysis Engine

The Analysis Engine determines:

Is the item valid?

Is it obsolete?

Is it dangerous?

Can it be removed?

How much space will be released?

Will removal affect ?

Can rollback be supported?

Should AI intervene?

Every analysis result must include a confidence score.

Low-confidence items must never be automatically removed.

---

# Recommendation Engine

The Recommendation Engine converts technical analysis into user-friendly suggestions.

Instead of:

Delete HKCU Registry Key

Display:

Unused software configuration can be safely removed.

Recommendations must include:

Reason

Expected Benefit

Risk

Rollback Availability

Confidence Score

Estimated Space Saved

Estimated Performance Gain

Users should understand every recommendation without technical knowledge.

---

# Execution Engine

The Execution Engine performs operations only after approval.

Supported operations:

Delete

Move

Backup

Compress

Restore

Disable

Enable

Optimize

Repair

Update

Every operation must support:

Logging

Progress

Cancellation

Retry

Rollback

Verification

No operation may bypass the Execution Engine.

---

# Rollback Engine

The Rollback Engine is responsible for recovering all reversible operations.

Supported rollback scenarios:

Registry Restore

↓

File Restore

↓

Startup Restore

↓

Service Restore

↓

Scheduled Task Restore

↓

Browser Configuration Restore

↓

Extension Restore

↓

Configuration Restore

↓

AI Optimization Rollback

↓

Batch Operation Rollback

Every destructive operation must first generate a rollback record.

Rollback records should contain:

Operation ID

Timestamp

Module

Operator

Affected Objects

Backup Location

Original State

Target State

Verification Result

Rollback Status

Rollback Expiration Time

Rollback records must support:

Search

Filter

Restore

Export

Delete

---

# History Engine

The History Engine records every operation performed within LeslieCleaner.

Categories include:

Scanning

Cleaning

Registry

Uninstall

Residual Cleaning

Startup Optimization

Privacy Cleaning

Browser Cleaning

Duplicate File Removal

Large File Processing

Shredder

Configuration Changes

AI Recommendations

Plugin Operations

Updates

System Maintenance

Each history record should include:

Operation ID

Start Time

End Time

Duration

Module

Operation Type

Items Affected

Result

Rollback Available

Log Reference

Memory Usage

CPU Usage

History data should support:

Keyword Search

Date Filter

Module Filter

Operation Type Filter

Export

Automatic Cleanup

---

# Notification Engine

The Notification Engine provides unified notification management.

Notification Types:

Information

Success

Warning

Error

Critical

Background Task Complete

Update Available

Maintenance Reminder

Security Warning

Notifications should support:

Popup

Notification Center

Silent Mode

Do Not Disturb

Grouping

History

User-defined Rules

Notification rendering must remain independent from business modules.

---

# Logging Engine

The Logging Engine provides centralized structured logging.

Supported outputs:

Local File

SQLite

 Event Log

JSON

Future Cloud Upload

Every module logs through one unified logging interface.

Log entries include:

Operation ID

Timestamp

Module

Thread

Severity

Message

Exception

Stack Trace

Performance Metrics

Machine Information

Session ID

Logs should support asynchronous writing.

Logging must never block the UI.

---

# Configuration Engine

All configuration is managed centrally.

Configuration Categories:

Application

UI

Performance

Scanning

Cleaning Rules

Registry Rules

Browser Rules

Privacy Rules

AI Rules

Plugins

Updates

Developer Options

Configuration priorities:

Runtime

↓

User Configuration

↓

Machine Configuration

↓

Default Configuration

Configuration changes should support:

Validation

Rollback

Version Upgrade

Import

Export

Encryption (where appropriate)

---

# Rule Engine

The Rule Engine is the core intelligence of LeslieCleaner.

It determines:

What can be scanned.

What can be cleaned.

What should be ignored.

What should be protected.

Rule Categories:

Cleaner Rules

Registry Rules

Residual Rules

Browser Rules

Privacy Rules

Duplicate Rules

Large File Rules

Startup Rules

Extension Rules

Driver Rules

Service Rules

AI Rules

Each rule should support:

Priority

Condition

Action

Description

Risk Level

Rollback Support

Version

Author

Digital Signature (future)

Rules should be loaded dynamically.

No recompilation should be required for updating rule sets.

---

# Event Bus

Modules communicate using an Event Bus.

Example:

Cleaner

↓

ScanCompletedEvent

↓

Event Bus

↓

Dashboard

↓

History

↓

Notification

↓

AI Assistant

Cleaner must never directly call Dashboard.

Dashboard must never directly call Cleaner.

Loose coupling is mandatory.

Supported event types:

Scan Started

Scan Progress

Scan Completed

Analysis Completed

Execution Started

Execution Progress

Execution Completed

Rollback Started

Rollback Completed

Update Installed

Configuration Changed

Plugin Loaded

Plugin Unloaded

AI Recommendation Generated

---

# Command Bus

Business operations should be represented as Commands.

Examples:

StartSmartScanCommand

CleanRegistryCommand

AnalyzeLargeFilesCommand

DisableStartupItemCommand

UninstallProgramCommand

RestoreBackupCommand

Commands should contain only the information required to execute an action.

Commands should be immutable.

Each command should have one handler.

---

# Query Layer

Read operations should be separated from write operations.

Examples:

GetDashboardStatisticsQuery

GetRegistryProblemsQuery

GetStartupItemsQuery

GetHistoryQuery

GetDuplicateFilesQuery

Queries should never modify state.

Queries should be optimized for performance.

Large datasets should support paging and virtualization.

---

# Scheduler Engine

Background tasks are coordinated by the Scheduler Engine.

Supported task types:

Automatic Cleaning

Scheduled Scan

Update Check

Database Optimization

Rule Synchronization

History Cleanup

Log Cleanup

Backup Verification

Health Check

Task execution modes:

Manual

Scheduled

Event Triggered

Idle Triggered

Startup Triggered

Shutdown Triggered

Tasks should support:

Pause

Resume

Retry

Cancel

Priority

Dependencies

Timeout

---

# Task Priority Model

Every task belongs to one priority level.

Critical

High

Normal

Low

Background

Critical tasks always execute first.

Background tasks must never affect user interaction.

Long-running operations should automatically reduce priority when the user begins interacting with the application.

---

# Thread Architecture

Dedicated thread pools should exist for:

UI

↓

Scanning

↓

Database

↓

Logging

↓

Network

↓

AI Analysis

↓

Background Maintenance

Avoid sharing long-running work across unrelated thread pools.

Blocking operations on the UI thread are strictly prohibited.

---

# Database Architecture

LeslieCleaner uses SQLite as the default embedded database.

The database stores only application metadata and never user documents.

Database responsibilities include:

Configuration

↓

Cleaning History

↓

Rollback Records

↓

Rule Metadata

↓

Application Inventory

↓

Startup Inventory

↓

Browser Inventory

↓

Statistics

↓

Performance Metrics

↓

AI Recommendation Cache

↓

Plugin Metadata

↓

Notification Records

↓

Task Scheduler

User personal files must never be stored in the database.

---

# Database Design Principles

The database must satisfy:

Normalization

Index Optimization

Transaction Safety

Schema Versioning

Automatic Migration

Automatic Integrity Check

Backup Support

Recovery Support

Encryption Ready

Every schema modification must include a migration strategy.

Schema upgrades must never destroy existing user data.

---

# Repository Architecture

Business modules must never directly access SQLite.

Access path:

Application

↓

Repository Interface

↓

Repository Implementation

↓

SQLite

Example:

IRegistryRepository

↓

RegistryRepository

↓

SQLite

Repositories expose business-oriented interfaces rather than SQL details.

---

# Transaction Rules

Every write operation should execute within a transaction.

Supported transaction scenarios:

Registry Backup Record

↓

History Record

↓

Rollback Record

↓

Execution Result

↓

Commit

If any step fails:

Rollback Transaction

↓

Recover State

↓

Log Failure

↓

Notify User

Partial commits are prohibited.

---

# Cache Architecture

LeslieCleaner provides a unified cache service.

Cache Layers:

Memory Cache

↓

Disk Cache

↓

Persistent Cache

Cache Categories:

Rule Cache

Configuration Cache

Icon Cache

Application Metadata Cache

AI Recommendation Cache

Statistics Cache

Browser Profile Cache

Duplicate File Cache

Large File Cache

Every cache must define:

Maximum Capacity

Expiration Time

Invalidation Strategy

Refresh Policy

Thread Safety

No unlimited cache is permitted.

---

# Memory Management Architecture

Memory management follows:

Allocate Late

↓

Reuse Objects

↓

Dispose Early

↓

Monitor Continuously

Large collections should support:

Virtualization

Pagination

Streaming

Incremental Loading

Large files should never be fully loaded into memory unless absolutely necessary.

---

# File Scanning Pipeline

Every scan follows a unified processing pipeline.

Discover

↓

Filter

↓

Normalize

↓

Classify

↓

Analyze

↓

Score

↓

Generate Recommendation

↓

Display Result

↓

Await User Decision

↓

Execute

↓

Verify

↓

Record History

↓

Generate Rollback

No module may bypass this pipeline.

---

# Smart Scan Pipeline

Smart Scan coordinates multiple scanning engines.

Modules included:

Cleaner

Registry

Browser

Privacy

Startup

Residual

Temporary Files

Recycle Bin

Logs

The pipeline should execute concurrently where possible.

Each module reports progress independently.

Overall progress is aggregated automatically.

Failures in one module must not interrupt others.

---

# Residual Scan Engine

The Residual Scan Engine is one of the core competitive advantages of LeslieCleaner.

It must outperform traditional uninstallers.

Detection targets include:

Remaining Files

Remaining Directories

Remaining Registry Keys

Remaining Services

Remaining Drivers

Remaining Scheduled Tasks

Remaining Startup Entries

Remaining Context Menu Items

Remaining COM Components

Remaining CLSID Entries

Remaining Shell Extensions

Remaining Environment Variables

Remaining Firewall Rules

Remaining Shortcuts

Remaining Browser Extensions

Remaining User Data (Optional)

Detection Strategy:

Installed Program Database

↓

 Registry

↓

Known Install Locations

↓

Shell Registration

↓

Shortcut Analysis

↓

Service Analysis

↓

Driver Analysis

↓

USN Journal

↓

Signature Matching

↓

Heuristic Analysis

↓

AI Analysis

Scanning must use parallel processing.

Disk traversal should avoid redundant recursion.

Known directories should be indexed.

Hash-based duplicate detection should reduce repeated analysis.

The engine must prioritize speed without sacrificing accuracy.

---

# Registry Architecture

Registry scanning is rule-driven.

Registry Categories:

Application

COM

ActiveX

File Association

Fonts

Shared DLL

Startup

Services

Explorer

Shell

Context Menu

User Profile

MRU

Temporary Entries

Each rule includes:

Description

Risk

Recommendation

Rollback Support

Confidence

Estimated Benefit

Registry modifications are never executed directly.

They always pass through:

Backup

↓

Verification

↓

Execution

↓

Verification

↓

History

↓

Rollback Record

---

# Startup Manager Architecture

Startup sources include:

Run

RunOnce

Startup Folder

Task Scheduler

 Services

Explorer Extensions

Browser Startup

Drivers

Each startup item should contain:

Name

Publisher

Digital Signature

File Path

Command Line

Startup Impact

Boot Delay Estimate

CPU Usage

Memory Usage

Recommendation

Category

Category Rules:

Microsoft 

Hardware Vendor

Trusted Third-party

User Installed

Unknown

Potentially Suspicious

Microsoft system startup items must be protected by default.

Disabling protected startup items requires additional confirmation.

---

# Application Inventory Engine

Every installed application is normalized into a unified model.

Collected Information:

Display Name

Publisher

Version

Install Date

Install Size

Installation Path

Uninstall Command

Architecture

Digital Signature

Running Processes

Associated Services

Associated Drivers

Associated Scheduled Tasks

Associated Startup Items

Associated Browser Extensions

Associated Registry Entries

Associated User Data

This inventory serves as the foundation for:

Uninstaller

Residual Scanner

AI Recommendation

Dependency Analysis

---

# AI Assistant Architecture

The AI Assistant is a first-class platform capability.

It is not merely a chat interface.

Its responsibilities include:

System Analysis

↓

Cleaning Recommendation

↓

Registry Risk Analysis

↓

Startup Optimization

↓

Residual Analysis

↓

Duplicate Detection Assistance

↓

Large File Recommendation

↓

Privacy Recommendation

↓

Performance Diagnosis

↓

Update Recommendation

↓

User Education

↓

Operation Explanation

↓

Natural Language Search

The AI Assistant must remain decoupled from business modules.

Business modules provide structured data.

The AI Assistant interprets the data and generates recommendations.

---

# AI Pipeline

Every AI request follows a unified workflow.

Collect Context

↓

Collect System Information

↓

Collect Module Result

↓

Normalize Data

↓

Generate Prompt Context

↓

AI Analysis

↓

Confidence Evaluation

↓

Generate Recommendation

↓

Record History

↓

Display Explanation

AI must never directly execute dangerous operations.

Execution always requires explicit user confirmation.

---

# AI Context Provider

Each module exposes structured context.

Example:

Registry

↓

Invalid Keys

↓

Risk Level

↓

Rollback Support

↓

Affected Programs

↓

Estimated Benefit

Cleaner

↓

Temporary Files

↓

Recycle Bin

↓

Logs

↓

Browser Cache

↓

Space Saved

Startup

↓

Impact Score

↓

Publisher

↓

Boot Delay

↓

Recommendation

The AI consumes structured data rather than raw operating system information.

---

# Plugin Architecture

The Plugin System extends LeslieCleaner without modifying the Core.

Plugin Categories:

Cleaner Plugin

↓

Browser Plugin

↓

Cloud Storage Plugin

↓

Archive Plugin

↓

Developer Tool Plugin

↓

Enterprise Plugin

↓

AI Plugin

↓

Diagnostic Plugin

↓

Import/Export Plugin

↓

Visualization Plugin

Plugins must implement official SDK interfaces.

Plugins are loaded dynamically.

Plugins can be enabled or disabled independently.

---

# Plugin Lifecycle

Plugin Discovery

↓

Signature Verification

↓

Compatibility Check

↓

Dependency Check

↓

Load

↓

Initialize

↓

Register Services

↓

Register Commands

↓

Register UI

↓

Running

↓

Unload

↓

Release Resources

Plugins must release all resources before unloading.

---

# Plugin Isolation

Plugins must never directly access:

Core Database

↓

Core Memory

↓

Internal Services

↓

Private APIs

↓

  Native Layer

Plugins communicate only through:

SDK

↓

Event Bus

↓

Command Bus

↓

Public Service Interfaces

Faulty plugins must not crash the application.

---

#   Platform Abstraction Layer

The Platform Layer abstracts all   APIs.

Examples:

Registry

↓

RegistryService

File System

↓

FileService

Shell

↓

ShellService

Task Scheduler

↓

TaskSchedulerService

Service Manager

↓

 ServiceService

COM

↓

ComService

Native API usage must never appear inside business modules.

---

# File System Architecture

File operations use a unified abstraction.

Supported Features:

Async IO

↓

Buffered IO

↓

Memory Mapped File

↓

Streaming

↓

Large File Support

↓

Cancellation

↓

Retry

↓

Verification

↓

Progress Reporting

↓

Permission Handling

No module may directly call raw file APIs.

---

# High Performance Scan Architecture

Scanning performance is a major competitive advantage.

Scanning priority:

USN Journal

↓

MFT Analysis (Future)

↓

Indexed Directory

↓

Known Locations

↓

Heuristic Discovery

↓

Recursive Scan (Last Resort)

Avoid full recursive traversal whenever possible.

Leverage NTFS metadata for acceleration.

---

# Multi-thread Scan Model

Scanning tasks are decomposed into independent workers.

Coordinator

↓

Directory Scanner

↓

Registry Scanner

↓

Browser Scanner

↓

Residual Scanner

↓

Analyzer

↓

Aggregator

↓

UI Progress

Each worker operates independently.

The coordinator schedules execution.

Task stealing should be supported for load balancing.

---

# File Shredder Architecture

The File Shredder supports secure deletion.

Deletion Levels:

Fast Delete

↓

Single Pass Overwrite

↓

Three Pass Overwrite

↓

Seven Pass Overwrite

↓

Custom Pass Count

Features:

Folder Shredding

↓

Free Space Wiping

↓

Filename Obfuscation

↓

Metadata Cleanup

↓

Verification

Shredding operations cannot be rolled back.

Additional confirmation is required.

---

# Duplicate File Detection Engine

Duplicate detection occurs in multiple stages.

Stage 1

File Size

↓

Stage 2

Extension

↓

Stage 3

Partial Hash

↓

Stage 4

Full Hash

↓

Stage 5

Optional Binary Comparison

Hash algorithms should be configurable.

Large datasets should be processed incrementally.

Duplicate groups should be generated lazily.

---

# Large File Analyzer

The analyzer identifies files consuming excessive storage.

Classification:

Video

↓

Image

↓

Archive

↓

Installer

↓

ISO

↓

Database

↓

Virtual Machine

↓

Backup

↓

Other

Display information:

Size

↓

Last Access

↓

Last Modified

↓

Extension

↓

Location

↓

Owner

↓

Recommendation

Visualization support should be considered for future versions.

---

# Browser Cleaning Architecture

Each browser implements an independent adapter.

Supported browsers:

Microsoft Edge

Google Chrome

Mozilla Firefox

Brave

Opera

Vivaldi

Future browsers can be added without modifying the Core.

Cleaning Categories:

Cache

↓

Cookies

↓

History

↓

Download History

↓

Session

↓

Form Data

↓

Password (Optional)

↓

Site Permission

↓

Local Storage

↓

IndexedDB

↓

Service Worker Cache

Users must choose which data categories to remove.

---

# Privacy Protection Architecture

Privacy scanning includes:

Recent Documents

↓

Clipboard History (where supported)

↓

Temporary Files

↓

Search History

↓

Run History

↓

Jump Lists

↓

Thumbnail Cache

↓

Crash Dumps

↓

Application Logs

↓

Browser Privacy Data

↓

Telemetry Cache

Each privacy item should explain:

Why it exists

↓

Privacy Risk

↓

Storage Used

↓

Recommendation

↓

Rollback Availability

Users should clearly understand the consequences before deletion.

---

# Uninstaller Architecture

The Uninstaller is not merely a launcher for the application's uninstall command.

Its responsibility is to completely analyze, uninstall, verify and clean an application's footprint.

The complete workflow is:

Application Discovery

↓

Metadata Analysis

↓

Dependency Analysis

↓

Running Process Detection

↓

Service Detection

↓

Driver Detection

↓

Scheduled Task Detection

↓

Startup Detection

↓

Browser Extension Detection

↓

Registry Analysis

↓

Residual Prediction

↓

User Confirmation

↓

Official Uninstaller Execution

↓

Real-time Monitoring

↓

Residual Scan

↓

Residual Verification

↓

Cleanup

↓

Rollback Record

↓

History Record

↓

Completion

The entire process must be observable and recoverable.

---

# Uninstall Engine

The Uninstall Engine consists of several independent components.

Application Inventory Engine

↓

Dependency Analyzer

↓

Official Uninstaller Launcher

↓

Residual Detection Engine

↓

Residual Cleanup Engine

↓

Verification Engine

↓

Rollback Engine

↓

History Engine

Each engine has a single responsibility.

---

# Residual Detection Engine

The Residual Detection Engine is one of the most important competitive features of LeslieCleaner.

Primary Goal

Achieve significantly faster residual detection than traditional uninstallers while maintaining high accuracy.

Design Objectives

Fast

Accurate

Parallel

Incremental

Recoverable

Measurable

Scalable

---

# Residual Detection Sources

The engine combines multiple evidence sources.

Installed Application Metadata

↓

  Registry

↓

Known Installation Paths

↓

ProgramData

↓

AppData

↓

Program Files

↓

Program Files (x86)

↓

  Installer Database

↓

Shell Registration

↓

Environment Variables

↓

  Services

↓

Drivers

↓

Scheduled Tasks

↓

Firewall Rules

↓

Context Menu

↓

Browser Extensions

↓

Recent Usage

↓

USN Journal

↓

Known Vendor Rules

↓

AI Heuristic Analysis

The engine must combine these signals instead of relying on only one source.

---

# High-speed Residual Detection Strategy

Traditional uninstallers often perform recursive full-disk searches.

This approach is prohibited.

Instead, LeslieCleaner adopts a layered strategy.

Stage 1

Official Install Location

↓

Stage 2

Vendor Known Directories

↓

Stage 3

Registry Recorded Paths

↓

Stage 4

  Installer Database

↓

Stage 5

Application Signature Matching

↓

Stage 6

Indexed Directory Search

↓

Stage 7

USN Journal Verification

↓

Stage 8

Heuristic Scan

↓

Stage 9

Optional Deep Scan

Deep Scan should only execute when the user explicitly requests it.

The default workflow must prioritize speed.

---

# Parallel Residual Scanner

Residual scanning should be divided into multiple workers.

Worker A

Registry

Worker B

Files

Worker C

Folders

Worker D

Services

Worker E

Drivers

Worker F

Scheduled Tasks

Worker G

Shell Extensions

Worker H

Startup Entries

Worker I

Browser Data

The Scheduler coordinates all workers.

Workers report progress independently.

Final results are aggregated.

One slow worker must never block the others.

---

# Smart Residual Index

To reduce repeated scanning, LeslieCleaner maintains an internal index.

The index stores:

Application Name

Publisher

Version

Installation Directory

Common Residual Locations

Known Registry Locations

Known Services

Known Drivers

Known Startup Entries

Known Browser Extensions

Known Cache Locations

The index is updated automatically after every installation and uninstall.

This minimizes future scan time.

---

# Registry Cleaner Architecture

Registry cleaning is rule-based and confidence-driven.

Categories include:

Invalid Application Entries

↓

Unused File Associations

↓

Invalid Shared DLL References

↓

Invalid COM Registrations

↓

Invalid CLSID Entries

↓

Invalid Type Libraries

↓

Unused Fonts

↓

Obsolete Installer Records

↓

Broken Startup Entries

↓

Explorer Residual Items

↓

Shell Extension Residuals

↓

Context Menu Residuals

↓

Temporary Registry Data

Every detected item includes:

Category

Description

Risk Level

Confidence

Estimated Benefit

Rollback Support

Related Application

Users must always know why an entry is recommended for removal.

---

# Registry Backup Architecture

Before any registry modification:

Generate Restore Point (optional)

↓

Export Registry Branch

↓

Create Internal Backup

↓

Calculate Hash

↓

Store Metadata

↓

Execute Modification

↓

Verify Result

↓

Record History

↓

Enable Rollback

Rollback must restore the exact previous state.

---

# Startup Manager Architecture

Startup items must never appear in one mixed list.

Automatic categorization is mandatory.

Categories:



↓

Hardware Vendor

↓

Security Software

↓

Device Drivers

↓

Installed Applications

↓

User Added

↓

Unknown

↓

Potentially Suspicious

Critical Microsoft startup items are protected.

Protected items require additional confirmation before disabling.

---

# Startup Classification Engine

Classification combines multiple signals.

Digital Signature

↓

Publisher

↓

  Catalog

↓

Known Vendor Database

↓

Installation Source

↓

Registry Source

↓

Executable Location

↓

AI Classification

The classification result contains:

Category

Importance

Startup Impact

Risk

Recommendation

Estimated Boot Delay

Memory Usage

CPU Usage

Users should clearly distinguish system startup items from software startup items.

---

# Installer Architecture

LeslieCleaner provides two installation modes.

Express Installation

↓

Custom Installation

Custom Installation supports:

Installation Path Selection

↓

Disk Space Validation

↓

Create Desktop Shortcut

↓

Create Start Menu Shortcut

↓

Auto Update Option

↓

Launch After Installation

↓

Startup Option

↓

Language Selection (future)

↓

Component Selection (future)

The installer must support repair, modify and uninstall.

---

# Batch Operation Architecture

Every list-oriented module shares one unified batch framework.

Supported actions:

Select All

↓

Deselect All

↓

Invert Selection

↓

Batch Analyze

↓

Batch Delete

↓

Batch Restore

↓

Batch Ignore

↓

Batch Export

↓

Batch Enable

↓

Batch Disable

↓

Batch Rescan

Batch execution uses a task queue.

UI remains responsive throughout execution.

Large datasets should be virtualized.

---

# Unified Selection Framework

Every selectable list implements:

Single Selection

↓

Multi Selection

↓

Tri-state Checkbox

↓

Keyboard Selection

↓

Mouse Selection

↓

Shift Range Selection

↓

Ctrl Multi Selection

↓

Select by Filter

↓

Select by Category

Selection state is synchronized with batch commands automatically.

No module should implement its own independent selection logic.

---

# Performance Architecture

Performance optimization is built into the architecture rather than added later.

Optimization Targets:

Cold Start

Warm Start

Scan Speed

Memory Allocation

CPU Usage

Disk IO

Database Query

UI Rendering

Garbage Collection

Background Tasks

Every optimization must be measurable using benchmark tools.

---

# Resource Scheduling

The Resource Scheduler continuously balances system load.

Inputs include:

CPU Usage

↓

Memory Pressure

↓

Disk Queue Length

↓

Battery Status

↓

Foreground Activity

↓

User Interaction

↓

Background Tasks

When the system is busy:

Reduce background scan priority.

When the system is idle:

Increase maintenance throughput.

The application must adapt to the user's environment automatically.

---

# Memory Architecture

Memory efficiency is a first-class architectural concern.

Target Requirements:

Idle Memory

≤120 MB

Smart Scan Peak

≤350 MB

Deep Scan Peak

≤600 MB

Memory usage must remain stable during continuous execution.

No progressive memory growth is acceptable.

---

# Memory Allocation Strategy

Prefer:

Stack Allocation

↓

Object Pool

↓

Shared Immutable Objects

↓

Heap Allocation

Large temporary allocations should be avoided.

Reusable objects should be pooled whenever practical.

Supported object pools include:

String Builder Pool

↓

Byte Buffer Pool

↓

Scan Result Pool

↓

Hash Buffer Pool

↓

Task Context Pool

↓

Database Command Pool

↓

Image Cache Pool

Object pools must automatically reclaim unused instances.

---

# Garbage Collection Strategy

Reduce GC pressure by:

Avoiding unnecessary allocations

↓

Reusing buffers

↓

Streaming large files

↓

Incremental processing

↓

Avoiding excessive LINQ allocations

↓

Using Span<T> / Memory<T> where appropriate

Long-running scans should not trigger frequent Full GC.

---

# UI Architecture

The Presentation Layer adopts MVVM.

Components:

View

↓

ViewModel

↓

Command

↓

Application Service

↓

Domain

↓

Infrastructure

Rules:

View never contains business logic.

ViewModel never directly accesses   APIs.

Application Services coordinate business workflows.

Every ViewModel should be independently testable.

---

# Navigation Architecture

Navigation is centralized.

Navigation Service

↓

Navigation Stack

↓

Route Registry

↓

View Factory

↓

ViewModel Factory

Features:

Deep Navigation

↓

Back Navigation

↓

Forward Navigation

↓

State Preservation

↓

Parameter Passing

↓

Navigation History

Modules should never directly instantiate  .

---

# Design System

The entire application must share one Design System.

Foundation:

Color Palette

↓

Typography

↓

Spacing

↓

Radius

↓

Elevation

↓

Iconography

↓

Animation

↓

Component Library

↓

Interaction Rules

No page may define its own design language.

---

# UI Visual Style

Visual Style Target:

Premium

Modern

Minimal

Professional

Elegant

Bright

Comfortable

Primary Theme:

High-quality Warm White

Reference Style:

Twitter (X)

↓

Notion

↓

Raycast

↓

Arc Browser

Avoid:

Dark Gray Heavy Interfaces

↓

Old Win32 Style

↓

Cheap Security Software Style

↓

Excessive Gradients

↓

Heavy Borders

↓

Crowded Layout

The interface should communicate trust, speed and refinement.

---

# Component Library

Reusable Components:

Primary Button

↓

Secondary Button

↓

Danger Button

↓

Checkbox

↓

Tri-state Checkbox

↓

Radio Button

↓

Toggle Switch

↓

Search Box

↓

Data Grid

↓

Virtualized List

↓

Progress Bar

↓

Circular Progress

↓

Statistic Card

↓

Notification Card

↓

Dialog

↓

Bottom Sheet

↓

Toast

↓

Context Menu

↓

Command Palette

↓

File Picker

↓

Folder Picker

↓

Charts

↓

Empty State

↓

Loading State

↓

Skeleton Loading

↓

Error State

No page should create duplicated UI components.

---

# Theme Engine

The Theme Engine supports:

Light Theme

↓

Dark Theme (Future)

↓

System Theme

↓

Custom Accent Color (Future)

All colors originate from centralized design tokens.

Hardcoded colors are prohibited.

---

# Animation Architecture

Animation Principles:

Fast

↓

Subtle

↓

Consistent

↓

Interruptible

↓

Hardware Accelerated

Recommended durations:

100 ms

150 ms

200 ms

250 ms

Avoid animations longer than 300 ms.

Animations must never reduce productivity.

---

# Accessibility Architecture

Support:

High DPI

↓

Keyboard Navigation

↓

Screen Reader

↓

High Contrast

↓

Scalable Fonts

↓

Focus Indicators

↓

Accessible Color Contrast

Accessibility is mandatory.

---

# Update Architecture

The Update Engine consists of:

Version Service

↓

Update Checker

↓

Package Downloader

↓

Integrity Verifier

↓

Digital Signature Verifier

↓

Installer Launcher

↓

Rollback Manager

↓

Notification

Features:

Background Download

↓

Resume Download

↓

Incremental Update

↓

Automatic Retry

↓

Rollback

↓

Version History

Users may postpone updates.

Forced updates are not permitted except for critical security issues.

---

# Security Architecture

Security Layers:

Input Validation

↓

Permission Validation

↓

File Validation

↓

Registry Validation

↓

Digital Signature Verification

↓

Hash Verification

↓

Rollback

↓

Audit Logging

↓

Exception Monitoring

↓

Recovery

Security decisions should always favor user safety.

---

# Privacy Architecture

The application collects only operational data required for functionality.

Personal documents are never analyzed unless explicitly selected by the user.

No telemetry is enabled by default.

Any optional telemetry requires explicit opt-in.

Users must be able to:

View

↓

Export

↓

Delete

↓

Disable

their own application data.

---

# Enterprise Extension Architecture

Future Enterprise Edition may add:

Central Management

↓

Policy Distribution

↓

Asset Inventory

↓

Remote Maintenance

↓

Organization Licensing

↓

Cloud Synchronization

↓

Audit Reports

↓

Device Health Dashboard

Current architecture must reserve extension points for these capabilities.

---

# Benchmark Architecture

Benchmark modules measure:

Startup Time

↓

Shutdown Time

↓

Smart Scan Time

↓

Deep Scan Time

↓

Registry Scan Time

↓

Residual Scan Time

↓

Memory Usage

↓

CPU Usage

↓

Disk IO

↓

UI FPS

↓

GC Count

↓

Database Performance

Every release must include benchmark reports.

---

# Diagnostics Architecture

Diagnostic data includes:

Module Status

↓

Task Queue

↓

Background Workers

↓

Memory Snapshot

↓

Thread Snapshot

↓

Database Health

↓

Cache Statistics

↓

Configuration Validation

↓

Rule Version

↓

Plugin Status

Diagnostic information assists troubleshooting without exposing sensitive user data.

---

# System Component Relationship

Presentation Layer

↓

Application Layer

↓

Command Bus

↓

Event Bus

↓

Domain Layer

↓

Shared Engines

↓

Infrastructure Layer

↓

Platform Layer

Shared Engines include:

Scan Engine

Analysis Engine

Rule Engine

Recommendation Engine

Execution Engine

Rollback Engine

History Engine

Logging Engine

Configuration Engine

Notification Engine

Scheduler Engine

Plugin Engine

AI Assistant

All modules communicate only through the approved architecture.

Direct cross-module dependencies are prohibited.

---

# Future Architecture Principles

Future expansion must not require redesign of the core architecture.

New functionality should be introduced through:

New Modules

↓

New Rules

↓

New Plugins

↓

New Services

↓

New Adapters

instead of modifying existing stable modules.

Backward compatibility should be preserved whenever feasible.

---

# Final Architecture Goal

The LeslieCleaner architecture is designed to provide:

Commercial-grade Quality

↓

Enterprise Maintainability

↓

Excellent User Experience

↓

Exceptional Performance

↓

Low Resource Consumption

↓

High Stability

↓

High Security

↓

Excellent Scalability

↓

AI-native Capability

↓

Long-term Evolution

Every architectural decision must support these objectives.

This document defines the official technical architecture for the LeslieCleaner platform.

All implementations, modules, plugins and future releases must comply with this architecture.

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

