# LeslieCleaner Android Development Bible - 03_Project_Structure

> Android Prompt ID: AP-MAIN-03-PROJECT-STRUCTURE-MD  
> Source Prompt: `Main\03_Project_Structure.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-03-PROJECT-STRUCTURE-MD` in the Android traceability matrix.
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

Project Structure

Priority

★★★★★

This document defines the complete directory structure, project organization, naming conventions and resource layout for the LeslieCleaner solution.

All developers, AI assistants and plugins must comply with this document.

The directory structure is considered part of the software architecture.

No module may arbitrarily create folders outside the defined structure.

---

# Project Organization Philosophy

The project structure should satisfy:

Readable

↓

Scalable

↓

Maintainable

↓

Predictable

↓

Consistent

↓

Easy to Navigate

↓

Enterprise-grade

The folder hierarchy should clearly communicate responsibilities.

A developer should locate any file within seconds.

---

# Root Directory Structure

LeslieCleaner/

│
├── src/
├── docs/
├── tests/
├── assets/
├── installer/
├── scripts/
├── tools/
├── benchmarks/
├── publish/
├── build/
├── logs/
├── temp/
├── .github/
├── .editorconfig
├── .gitignore
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
├── LICENSE
└── CHANGELOG.md

No unrelated folders are permitted at the root level.

---

# Root Directory Responsibilities

src/

Application source code.

docs/

Architecture and technical documentation.

tests/

Automated testing projects.

assets/

Static resources.

installer/

Installer source and packaging.

scripts/

Automation scripts.

tools/

Internal development tools.

benchmarks/

Performance benchmark projects.

publish/

Release artifacts.

build/

Intermediate build scripts.

logs/

Development logs only.

temp/

Temporary development files.

No production code may exist outside src.

---

# Source Directory

src/

│
├── LeslieCleaner.App/
├── LeslieCleaner.UI/
├── LeslieCleaner.Application/
├── LeslieCleaner.Domain/
├── LeslieCleaner.Infrastructure/
├── LeslieCleaner.Platform/
├── LeslieCleaner.Shared/
├── LeslieCleaner.SDK/
├── LeslieCleaner.Plugins/
├── LeslieCleaner.Resources/
└── LeslieCleaner.Localization/

Every project has a single responsibility.

---

# LeslieCleaner.App

Responsibilities:

Program Entry

Host Initialization

Dependency Injection Registration

Configuration Loading

Application Startup

Global Exception Handler

Window Bootstrap

No business logic.

---

# LeslieCleaner.UI

Contains only presentation logic.

Structure:

Views/

↓

ViewModels/

↓

Controls/

↓

Converters/

↓

Behaviors/

↓

Animations/

↓

Styles/

↓

Themes/

↓

Navigation/

↓

Dialogs/

↓

Components/

↓

Pages/

↓

Resources/

Business logic is prohibited.

---

# LeslieCleaner.Application

Contains:

Use Cases

Commands

Queries

DTOs

Validators

Workflow Coordinators

Application Events

Permissions

Transactions

No   API.

No UI.

---

# LeslieCleaner.Domain

Contains:

Entities

Value Objects

Aggregates

Domain Services

Specifications

Business Policies

Interfaces

Domain Events

Business rules belong here.

No infrastructure implementation.

---

# LeslieCleaner.Infrastructure

Contains:

Repositories

SQLite

Logging

Cache

Configuration

Serialization

Rule Loader

Update Service

Backup Service

History Service

Notification Service

Infrastructure adapters only.

---

# LeslieCleaner.Platform

Responsible for  -specific capabilities.

Contains:

Registry

↓

File System

↓

Shell

↓

COM

↓

Task Scheduler

↓

Service Manager

↓

WMI

↓

ETW

↓

Performance Counter

↓

USN Journal

↓

Native API Wrappers

No business logic.

Only platform abstraction.

---

# LeslieCleaner.Shared

Contains shared components.

Examples:

Common Models

↓

Result<T>

↓

Constants

↓

Enums

↓

Extensions

↓

Utilities

↓

Global Interfaces

↓

Exceptions

Only reusable code belongs here.

Avoid turning Shared into a dumping ground.

---

# LeslieCleaner.SDK

Contains the public Plugin SDK.

Folders:

Abstractions/

↓

Contracts/

↓

Events/

↓

Commands/

↓

Plugin Base/

↓

Versioning/

↓

Documentation/

Internal implementations are prohibited.

Only public APIs.

---

# LeslieCleaner.Plugins

Structure:

BuiltIn/

↓

Official/

↓

Experimental/

↓

Samples/

↓

Templates/

Third-party plugins are loaded externally and must not be stored here in production.

---

# LeslieCleaner.Resources

Contains application resources.

Icons/

↓

Images/

↓

Fonts/

↓

Illustrations/

↓

Lottie/

↓

SVG/

↓

Theme Assets/

↓

Logos/

Resources should be optimized before inclusion.

Duplicate assets are prohibited.

---

# LeslieCleaner.Localization

Contains language resources.

Example:

en-US/

↓

zh-CN/

↓

ja-JP/

↓

fr-FR/

↓

de-DE/

↓

future languages...

All user-facing strings must originate from localization resources.

Hardcoded UI text is prohibited.

---

# Documentation Structure

docs/

│
├── architecture/
├── api/
├── modules/
├── ui/
├── performance/
├── security/
├── testing/
├── release/
├── plugin-sdk/
└── ai-prompts/

Every document belongs to a specific category.

Architecture documents should never be mixed with user documentation.

---

---

# Application Module Structure

All business modules inside LeslieCleaner.Application must follow a unified structure.

Example:

LeslieCleaner.Application/

│
├── Cleaner/
├── Registry/
├── Uninstaller/
├── Startup/
├── Privacy/
├── Browser/
├── FileAnalyzer/
├── Shredder/
├── DuplicateFinder/
├── Update/
├── AI/
└── Dashboard/

Each module represents one business capability.

---

# Module Internal Structure

Every module should follow:

ModuleName/

│
├── Commands/
├── Queries/
├── Services/
├── DTOs/
├── Models/
├── Validators/
├── Events/
├── Handlers/
└── Extensions/

Responsibilities must remain clear.

---

# Cleaner Module Structure

Example:

Cleaner/

│
├── Commands/
│   ├── StartCleanCommand.cs
│   └── ExecuteCleanCommand.cs
│
├── Queries/
│   ├── GetCleanItemsQuery.cs
│   └── GetCleanStatisticsQuery.cs
│
├── Services/
│   ├── CleanAnalyzer.cs
│   ├── CleanExecutor.cs
│   └── CleanRecommendationService.cs
│
├── Rules/
│
├── Models/
│
└── Events/


Cleaner module responsibilities:

Temporary File Cleaning

↓

Cache Cleaning

↓

Log Cleaning

↓

Recycle Bin Cleaning

↓

Application Junk Cleaning

No registry logic.

No uninstall logic.

---

# Registry Module Structure

Registry/

│
├── Commands/
├── Queries/
├── Services/
│
├── Rules/
│
├── Backup/
│
├── Analysis/
│
├── Models/
│
└── Events/


Responsibilities:

Registry Scan

↓

Risk Analysis

↓

Backup

↓

Cleanup

↓

Restore

Registry operations must use Platform Layer.

---

# Uninstaller Module Structure

Uninstaller/

│
├── Discovery/
├── Analysis/
├── Execution/
├── Residual/
├── Backup/
├── Verification/
├── Models/
└── Events/


Responsibilities:

Application Discovery

↓

Uninstall Execution

↓

Residual Detection

↓

Residual Cleanup

↓

Verification

---

# Startup Manager Structure

Startup/

│
├── Sources/
│
├── Classification/
│
├── Analysis/
│
├── Commands/
│
├── Queries/
│
├── Models/
│
└── Events/


Startup sources:

Registry Run

↓

Startup Folder

↓

Task Scheduler

↓

Services

↓

Drivers

↓

Explorer Extensions

---

# Privacy Module Structure

Privacy/

│
├── Scanner/
├── Rules/
├── Analyzer/
├── Cleaner/
├── Backup/
├── Models/
└── Events/


Privacy categories:

Browser Privacy

↓

Recent Files

↓

History

↓

Cache

↓

Temporary Data

↓

System Traces

---

# Browser Cleaner Structure

Browser/

│
├── Abstractions/
├── Chrome/
├── Edge/
├── Firefox/
├── Brave/
├── Opera/
├── Profiles/
├── Rules/
└── Models/


Every browser must implement:

IBrowserCleaner

IBrowserDetector

IBrowserProfileProvider

New browsers should be added without modifying Core.

---

# File Analyzer Structure

FileAnalyzer/

│
├── Scanner/
├── Index/
├── Categorizer/
├── Analyzer/
├── Visualization/
├── Models/
└── Events/


Responsibilities:

Large File Analysis

↓

Storage Visualization

↓

File Classification

↓

Recommendation

---

# Duplicate Finder Structure

DuplicateFinder/

│
├── Scanner/
├── Hash/
├── Comparison/
├── Grouping/
├── Models/
└── Events/


Processing:

Size Filtering

↓

Partial Hash

↓

Full Hash

↓

Binary Comparison

↓

Duplicate Group Generation

---

# Shredder Structure

Shredder/

│
├── Algorithms/
├── Execution/
├── Verification/
├── SecureDelete/
├── Models/
└── Events/


Supported algorithms:

Fast Delete

↓

Overwrite

↓

Multi-pass

↓

Custom

---

# AI Module Structure

AI/

│
├── Providers/
├── Prompts/
├── Context/
├── Analysis/
├── Recommendation/
├── Memory/
├── Models/
└── Events/


Responsibilities:

AI Provider Management

↓

Prompt Management

↓

Context Collection

↓

Recommendation Generation

↓

Explanation Generation

---

# AI Prompt Storage Structure

ai-prompts/

│
├── system/
│
├── modules/
│   ├── cleaner/
│   ├── registry/
│   ├── uninstall/
│   ├── startup/
│   └── privacy/
│
├── optimization/
│
├── diagnostics/
│
└── templates/


Every AI prompt must be version-controlled.

---

# Rule Database Structure

rules/

│
├── cleaner/
├── registry/
├── browser/
├── privacy/
├── startup/
├── residual/
├── duplicate/
└── security/


Each rule file contains:

Rule ID

Name

Description

Category

Risk Level

Confidence

Conditions

Actions

Version

Author

Last Updated

---

# Configuration Directory

Configuration/

│
├── Default/
├── User/
├── Machine/
├── Migration/
└── Backup/


Configuration priority:

Runtime

↓

User

↓

Machine

↓

Default

---

# Database Directory

data/

│
├── LeslieCleaner.db
├── Backup/
├── Migration/
└── Cache/


Database files must never be stored beside executable files.

---

# Log Directory

logs/

│
├── Application/
├── Security/
├── Performance/
├── Crash/
└── Audit/


Logs must support automatic rotation.

---

# Temporary Directory

temp/

Used only for:

Temporary Scan Data

↓

Intermediate Files

↓

Download Cache

↓

Update Packages

Temporary data must be cleaned automatically.

Production installation must not include this directory.

---
---

# Installer Directory Structure

The installer is an independent engineering project.

Directory:

installer/

│
├── Bootstrapper/
├── InnoSetup/
├── Resources/
├── Scripts/
├── Config/
├── Upgrade/
├── Uninstall/
└── Documentation/

---

# Bootstrapper

Responsibilities:

Environment Detection

↓

.NET Runtime Detection

↓

Permission Detection

↓

Installation Preparation

↓

Installer Launch

↓

Error Recovery

The Bootstrapper should remain lightweight.

---

# InnoSetup Directory

Contains:

Main Installer Script

↓

Language Files

↓

Installation Tasks

↓

Registry Entries

↓

Shortcut Configuration

↓

File Association

↓

Upgrade Logic

↓

Uninstall Logic

Installer scripts must be version-controlled.

---

# Installer Resources

Resources include:

Installer Logo

↓

Background Images

↓

Icons

↓

License Files

↓

Localization Files

All resources must be optimized.

---

# Installer Script Rules

Installer scripts must support:

Custom Installation Path

↓

Express Installation

↓

Component Selection

↓

Shortcut Selection

↓

Startup Option

↓

Repair Installation

↓

Modify Installation

↓

Clean Uninstall

↓

Upgrade

---

# Build Directory Structure

build/

│
├── Debug/
├── Release/
├── Scripts/
├── Artifacts/
└── Reports/

---

# Build Rules

Build directory contains only generated files.

Source code must never exist inside build.

Generated files should be automatically cleaned.

---

# Publish Directory Structure

publish/

│
├── Version/
│
├── Installer/
│
├── Portable/
│
├── Symbols/
│
└── Reports/


Example:

publish/

└── v1.0.0/

    ├── LeslieCleaner.exe

    ├── Installer.exe

    ├── ReleaseNotes.md

    └── Checksums.txt


---

# Test Project Structure

tests/

│
├── LeslieCleaner.UnitTests/
├── LeslieCleaner.IntegrationTests/
├── LeslieCleaner.UITests/
├── LeslieCleaner.SecurityTests/
└── LeslieCleaner.StressTests/

---

# Unit Test Structure

Unit tests follow source structure.

Example:

Source:

Cleaner/

↓

Test:

CleanerTests/

Tests:

ScannerTests

AnalyzerTests

ExecutorTests

RuleTests

---

# Integration Test Structure

Integration tests verify:

Database

↓

File System

↓

Registry

↓

  API

↓

Module Communication

↓

Workflow

Integration tests must run in controlled environments.

---

# UI Test Structure

UI tests verify:

Navigation

↓

Interaction

↓

Button Behavior

↓

Dialog

↓

Error Display

↓

Loading State

↓

Responsive Layout

↓

High DPI

---

# Security Test Structure

Security tests verify:

Permission

↓

Path Validation

↓

Registry Safety

↓

File Safety

↓

Rollback

↓

Injection Prevention

↓

Privilege Escalation Protection

---

# Stress Test Structure

Stress tests verify:

Large Disk Scan

↓

Millions Of Files

↓

Large Registry

↓

Thousands Startup Items

↓

Large History Database

↓

Long Running Sessions

---

# Benchmark Directory Structure

benchmarks/

│
├── Startup/
├── Scanner/
├── Registry/
├── Database/
├── Memory/
├── UI/
└── Reports/

---

# Benchmark Requirements

Every benchmark must record:

Environment

↓

Hardware

↓

OS Version

↓

Software Version

↓

Test Data Size

↓

Execution Time

↓

Memory Usage

↓

CPU Usage

↓

Result


Benchmark results must be reproducible.

---

# CI/CD Directory Structure

.github/

│
├── workflows/
│
├── ISSUE_TEMPLATE/
│
├── PULL_REQUEST_TEMPLATE.md
│
└── CODEOWNERS

---

# GitHub Actions Structure

.github/workflows/

│
├── build.yml

├── test.yml

├── benchmark.yml

├── security.yml

├── release.yml

└── package.yml


---

# CI Pipeline

Every commit should execute:

Restore Dependencies

↓

Build

↓

Unit Test

↓

Integration Test

↓

Security Scan

↓

Code Analysis

↓

Package Verification

---

# File Naming Rules

Files must use:

PascalCase

Examples:

CleanEngine.cs

RegistryScanner.cs

StartupManager.cs


Avoid:

clean_engine.cs

registry-scanner.cs

test123.cs

---

# Class Naming Rules

Classes:

Noun

Example:

CleanerService

RegistryScanner

StartupAnalyzer


Avoid:

DoCleaner

ProcessSomething

ManagerEverything

---

# Interface Naming Rules

Interfaces must begin with:

I

Examples:

ICleanService

IRegistryProvider

IPlugin

---

# Service Naming Rules

Services:

XXXService

Examples:

UpdateService

BackupService

HistoryService


---

# Repository Naming Rules

Repositories:

XXXRepository

Examples:

ApplicationRepository

RuleRepository

HistoryRepository

---

# Factory Naming Rules

Factories:

XXXFactory

Examples:

PluginFactory

ScannerFactory

ViewModelFactory

---

# Manager Naming Rules

Avoid excessive Manager classes.

Forbidden:

GlobalManager

SystemManager

EverythingManager

Managers are allowed only when they truly manage lifecycle.

---

# Model Naming Rules

Data models:

XXXModel

Examples:

StartupItemModel

CleanResultModel

RegistryIssueModel

---

# DTO Naming Rules

DTO:

XXXDto

Examples:

CleanItemDto

ApplicationDto

ScanResultDto

---

# Event Naming Rules

Events must end with:

Event

Examples:

ScanCompletedEvent

CleanStartedEvent

RollbackFinishedEvent

---

# Command Naming Rules

Commands end with:

Command

Examples:

StartScanCommand

DeleteFileCommand

RestoreBackupCommand

---

# Query Naming Rules

Queries end with:

Query

Examples:

GetHistoryQuery

GetStartupItemsQuery

GetStatisticsQuery

---

# Exception Naming Rules

Exceptions end with:

Exception

Examples:

ScanFailedException

RegistryAccessException

PluginLoadException

---
---

# Complete LeslieCleaner Project Tree

The following structure represents the official long-term project organization.

All future modules should follow this structure.

```
LeslieCleaner/

│
├── src/
│   │
│   ├── LeslieCleaner.App/
│   │   ├── Program.cs
│   │   ├── Startup/
│   │   ├── DependencyInjection/
│   │   ├── Configuration/
│   │   └── Exceptions/
│   │
│   ├── LeslieCleaner.UI/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Controls/
│   │   ├── Components/
│   │   ├── Navigation/
│   │   ├── Dialogs/
│   │   ├── Themes/
│   │   ├── Styles/
│   │   ├── Animations/
│   │   └── Resources/
│   │
│   ├── LeslieCleaner.Application/
│   │   ├── Cleaner/
│   │   ├── Registry/
│   │   ├── Uninstaller/
│   │   ├── Startup/
│   │   ├── Privacy/
│   │   ├── Browser/
│   │   ├── FileAnalyzer/
│   │   ├── DuplicateFinder/
│   │   ├── Shredder/
│   │   ├── AI/
│   │   ├── Update/
│   │   └── Dashboard/
│   │
│   ├── LeslieCleaner.Domain/
│   │   ├── Entities/
│   │   ├── ValueObjects/
│   │   ├── Aggregates/
│   │   ├── Specifications/
│   │   ├── Events/
│   │   └── Policies/
│   │
│   ├── LeslieCleaner.Infrastructure/
│   │   ├── Database/
│   │   ├── Repositories/
│   │   ├── Cache/
│   │   ├── Logging/
│   │   ├── Configuration/
│   │   ├── Backup/
│   │   ├── Update/
│   │   └── Serialization/
│   │
│   ├── LeslieCleaner.Platform/
│   │   ├── Registry/
│   │   ├── FileSystem/
│   │   ├── Shell/
│   │   ├── Services/
│   │   ├── TaskScheduler/
│   │   ├── COM/
│   │   ├── WMI/
│   │   └── Native/
│   │
│   ├── LeslieCleaner.Shared/
│   │   ├── Constants/
│   │   ├── Extensions/
│   │   ├── Models/
│   │   ├── Interfaces/
│   │   ├── Results/
│   │   └── Utilities/
│   │
│   ├── LeslieCleaner.SDK/
│   │   ├── Contracts/
│   │   ├── Interfaces/
│   │   ├── Events/
│   │   ├── Commands/
│   │   └── Documentation/
│   │
│   ├── LeslieCleaner.Plugins/
│   │   ├── BuiltIn/
│   │   ├── Official/
│   │   ├── Experimental/
│   │   └── Templates/
│   │
│   ├── LeslieCleaner.Resources/
│   │   ├── Icons/
│   │   ├── Images/
│   │   ├── Fonts/
│   │   ├── SVG/
│   │   └── Illustrations/
│   │
│   └── LeslieCleaner.Localization/
│       ├── zh-CN/
│       ├── en-US/
│       └── ja-JP/
│
│
├── docs/
│   │
│   ├── architecture/
│   ├── api/
│   ├── modules/
│   ├── ui/
│   ├── performance/
│   ├── security/
│   ├── testing/
│   ├── release/
│   ├── plugin-sdk/
│   └── ai-prompts/
│
│
├── tests/
│   │
│   ├── UnitTests/
│   ├── IntegrationTests/
│   ├── UITests/
│   ├── SecurityTests/
│   └── StressTests/
│
│
├── assets/
│   ├── Branding/
│   ├── Icons/
│   ├── Screenshots/
│   └── Marketing/
│
│
├── installer/
│   │
│   ├── Bootstrapper/
│   ├── Scripts/
│   ├── Resources/
│   ├── Upgrade/
│   └── Uninstall/
│
│
├── scripts/
│   ├── Build/
│   ├── Deploy/
│   ├── Cleanup/
│   ├── Database/
│   └── Automation/
│
│
├── tools/
│   ├── Generators/
│   ├── Analyzers/
│   └── Development/
│
│
├── benchmarks/
│   ├── Startup/
│   ├── Scanner/
│   ├── Memory/
│   ├── Database/
│   └── Reports/
│
│
├── rules/
│   ├── Cleaner/
│   ├── Registry/
│   ├── Residual/
│   ├── Browser/
│   ├── Startup/
│   └── Privacy/
│
│
├── ai-prompts/
│   ├── System/
│   ├── Architecture/
│   ├── Modules/
│   ├── Optimization/
│   ├── Debug/
│   └── Testing/
│
│
├── publish/
│
├── build/
│
├── logs/
│
├── temp/
│
├── .github/
│
├── .gitignore
├── .editorconfig
├── README.md
├── LICENSE
└── CHANGELOG.md
```

---

# AI Prompt Organization Rules

All AI development prompts must be stored separately.

Structure:

ai-prompts/

```
System/

├── Master_System_Prompt.md
├── Coding_Rules.md
└── Architecture_Rules.md


Modules/

├── Cleaner_Prompt.md
├── Registry_Prompt.md
├── Uninstaller_Prompt.md
├── Startup_Prompt.md
├── Privacy_Prompt.md
└── Browser_Prompt.md


Optimization/

├── Performance.md
├── Memory.md
└── Refactoring.md


Testing/

├── Unit_Test.md
├── Stress_Test.md
└── Security_Test.md
```

---

# Prompt Version Management

Every Prompt must contain:

Version

↓

Created Date

↓

Updated Date

↓

Purpose

↓

Applicable Module

↓

Input Requirements

↓

Output Requirements

↓

Change History


AI prompts are treated as engineering documents.

---

# Documentation Rules

Every module must have:

Architecture Document

↓

API Document

↓

Usage Document

↓

Testing Document

↓

Performance Document

↓

Troubleshooting Document

Documentation should be updated together with code changes.

---

# Third-party Dependency Structure

Third-party libraries must be documented.

Directory:

docs/dependencies/

Contains:

Library Name

↓

Version

↓

Purpose

↓

License

↓

Security Status

↓

Update History

↓

Replacement Plan

Unnecessary dependencies are prohibited.

---

# Development Environment Files

Root level development files:

```
.editorconfig

.gitignore

Directory.Build.props

Directory.Packages.props

global.json

README.md

CHANGELOG.md
```

These files must remain clean and maintained.

---

# Temporary File Rules

The following files are prohibited inside source directories:

*.tmp

*.bak

*.log

*.cache

*.old

*.copy

Temporary files must be stored in:

temp/

---

# Duplicate File Rules

Duplicate:

Code

↓

Resources

↓

Documents

↓

Configuration

are prohibited.

If shared:

Move to Shared Module.

---

# Structure Review Rules

Before adding any new folder, verify:

Does this responsibility already exist?

Can this belong to an existing module?

Will this increase complexity?

Does this violate architecture?

Is this required long term?

Only necessary structures should be introduced.

---

# Project Structure Acceptance Criteria

The structure is considered compliant when:

✓ Clear responsibility separation

✓ No circular dependencies

✓ No duplicated modules

✓ No random folders

✓ Documentation synchronized

✓ Tests organized

✓ Installer isolated

✓ AI prompts organized

✓ Resources optimized

✓ Build artifacts separated

✓ Third-party dependencies documented

✓ Future expansion supported

---

# Final Statement

This document defines the official LeslieCleaner project structure.

All source code, documentation, tests, resources, prompts and deployment files must follow this organization.

A clean project structure is the foundation of LeslieCleaner’s long-term development.

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

