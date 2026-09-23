# LeslieCleaner Android Development Bible - 01_Project_Rules

> Android Prompt ID: AP-MAIN-01-PROJECT-RULES-MD  
> Source Prompt: `Main\01_Project_Rules.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-01-PROJECT-RULES-MD` in the Android traceability matrix.
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

Version: 1.0

Document Type

Project Rules

Priority

★★★★★

This document defines the global development rules for the LeslieCleaner project.

Every developer, AI model, module and plugin must strictly comply with this document.

This document is second only to:

00_Master_System_Prompt.md

---

# Project Mission

LeslieCleaner is not merely a  cleaning tool.

It is an Enterprise-grade  Optimization Platform.

Its mission is:

Help users keep 

Cleaner

Faster

Safer

Smarter

More Stable

Longer-lasting

Every feature implemented must directly contribute to one or more of these goals.

Any feature that does not provide measurable user value must not be added.

---

# Product Principles

The LeslieCleaner project follows six core principles.

1.

Safety First

The software must never damage the user's operating system.

Never prioritize cleaning over safety.

2.

Performance First

Performance is one of the software's core selling points.

Every module must have measurable performance targets.

3.

Maintainability

Every line of code must be easy to understand.

Future developers should understand any module within minutes.

4.

Scalability

Today's architecture must support future expansion.

Never design for today's requirements only.

5.

Consistency

Every page.

Every module.

Every interaction.

Every animation.

Must follow one Design System.

6.

Production Ready

Everything delivered must be commercially releasable.

No experimental implementation.

No prototype implementation.

No educational implementation.

---

# Product Position

LeslieCleaner is positioned as:

AI Powered  Optimization Platform

Instead of:

Registry Cleaner

Cleaner Tool

PC Booster

System Optimizer

The software should gradually evolve into an intelligent  maintenance platform.

---

# Product Modules

Current planned modules include:

Dashboard

Smart Scan

Deep Clean

Registry Cleaner

Residual Cleaner

Smart Uninstaller

Browser Cleaner

Privacy Guard

Startup Manager

Service Manager

Driver Manager

Extension Manager

File Shredder

Duplicate Finder

Large File Analyzer

Disk Analyzer

Storage Visualization

AI Assistant

AI Optimization

History Center

Notification Center

Settings Center

Automatic Maintenance

Update Center

Plugin Center

Every module should remain independently maintainable.

---

# Design Philosophy

Users should never need to understand  internals.

LeslieCleaner must convert technical concepts into understandable actions.

Example:

Instead of:

Delete Registry CLSID

Display:

Remove Invalid Software Entries

Instead of:

Delete Orphan COM Objects

Display:

Clean Invalid Program Components

Always translate technical operations into user-friendly language.

---

# Software Characteristics

The software should always appear:

Reliable

Stable

Professional

Modern

Premium

Fast

Predictable

Transparent

Every operation should clearly explain:

What will happen.

Why it is recommended.

What risks exist.

How to recover.

---

# Global Architecture Rules

The project shall use layered architecture.

Presentation Layer

↓

Application Layer

↓

Domain Layer

↓

Infrastructure Layer

↓

Platform Layer

UI cannot directly access:

Database

Registry

File System

Network

Kernel

Every operation must pass through the Application Layer.

---

# Module Independence Rules

Each module is an independent bounded context.

Examples:

Registry Module

↓

Cannot directly call

Cleaner Module

Instead:

Registry

↓

Event

↓

Application Layer

↓

Cleaner

Every module communicates through contracts.

Never through implementation.

---

# Shared Service Rules

The following services must be global.

Logging

Configuration

Localization

Notification

Theme

Task Scheduler

Permission Manager

History

Cache

Rule Engine

No module should implement its own version of these services.

Only one implementation is allowed.

---

# Configuration Management

Every configuration item must support:

Read

Write

Import

Export

Reset

Backup

Restore

Configuration files should be versioned.

Older versions should automatically migrate.

Configuration corruption should be recoverable.

---

# Rule Engine Principle

LeslieCleaner is fundamentally a Rule-driven Platform.

Cleaning logic.

Registry logic.

Residual logic.

Browser logic.

Privacy logic.

Startup logic.

AI Recommendation.

Should all be driven by configurable rule engines.

Never hardcode cleaning rules into business logic.

Future rule updates should not require recompilation.


# 01_Project_Rules.md（Part 2）

---

# Project Directory Rules

The entire LeslieCleaner solution must strictly follow a unified directory structure.

No module is allowed to create arbitrary folders.

No temporary files may exist outside designated directories.

Recommended structure:

src/
docs/
tests/
assets/
installer/
scripts/
tools/
build/
publish/

Each directory has a single responsibility.

---

# Source Code Rules

Every project must have a clear purpose.

Example:

LeslieCleaner.App

Program Entry

LeslieCleaner.UI

Presentation Layer

LeslieCleaner.Core

Business Logic

LeslieCleaner.Infrastructure

Infrastructure

LeslieCleaner.Shared

Shared Models

LeslieCleaner.Data

Persistence

LeslieCleaner.Tests

Unit Tests

LeslieCleaner.Benchmark

Performance Tests

LeslieCleaner.SDK

Plugin SDK

No mixed responsibilities.

---

# Namespace Rules

Namespace hierarchy must always match the physical folder hierarchy.

Example:

LeslieCleaner.Core.Registry

↓

Folder

Core

↓

Registry

Never create inconsistent namespaces.

---

# Dependency Rules

Allowed dependency direction:

UI

↓

Application

↓

Domain

↓

Infrastructure

Forbidden:

Infrastructure

↓

UI

Forbidden:

Core

↓

ViewModel

Forbidden:

ViewModel

↓

Database

Forbidden:

Business Layer

↓

Window

↓

Control

↓

Button

No reverse dependencies.

---

# Single Responsibility Rule

One module

One purpose.

One class

One responsibility.

One method

One task.

Never create utility classes containing hundreds of unrelated methods.

Never create Manager classes that control the entire system.

---

# Global Coding Rules

Every source file must include:

XML documentation

Namespace

Using directives

Exception handling

Logging

Dependency injection support

Meaningful naming

Consistent formatting

Every public API must be documented.

---

# Business Logic Rules

Business logic must never appear in:

Window

UserControl

View

ResourceDictionary

Style

Animation

Converter

UI is only responsible for displaying data.

---

# Configuration Rules

Configuration files must be stored separately.

Never hardcode:

Path

Port

Threshold

Timeout

Language

Theme

Update URL

API Endpoint

License Information

Machine-specific values

Everything configurable must be external.

---

# File Operation Rules

All file operations must satisfy:

Async

Cancelable

Recoverable

Logged

Observable

Validated

Batch-capable

Never delete files directly.

Always move to Quarantine first unless the user explicitly chooses secure deletion.

---

# Registry Rules

Registry operations must follow:

Scan

↓

Verify

↓

Backup

↓

Analyze

↓

Present Result

↓

User Confirmation

↓

Modify

↓

Verify Again

↓

Log

↓

Rollback Available

Skipping any step is prohibited.

---

# Database Rules

SQLite is the default local database.

Every table must include:

Primary Key

Created Time

Updated Time

Version

Indexes

No unnecessary duplicate tables.

Indexes must be added for frequently queried fields.

---

# Rule Database

Cleaning logic must not be hardcoded.

Instead:

Residual Rules

Cleaner Rules

Browser Rules

Registry Rules

Privacy Rules

Startup Rules

AI Recommendation Rules

must all be stored in rule databases.

Rules should support online updates without recompiling the application.

---

# Logging Rules

Every operation must generate structured logs.

Each log should include:

Operation ID

Timestamp

Module

Action

Duration

Memory Usage

CPU Usage

Result

Exception (if any)

Logs must be searchable.

Logs must support automatic cleanup.

---

# Cache Rules

Cache is only allowed when measurable performance improvements exist.

Each cache must define:

Capacity

Expiration

Invalidation Strategy

Refresh Strategy

Thread Safety

Never implement unlimited cache.

---

# Localization Rules

All user-visible text must support localization.

Never hardcode UI strings.

Every text should originate from resource files.

Future language expansion should not require code modification.

---

# Resource Rules

Images

Icons

Fonts

Styles

Animations

Themes

must all be managed centrally.

Duplicate resources are prohibited.

Unused resources must be removed before Release.

---

# Plugin Rules

Every plugin must communicate only through the official SDK.

Plugins may never directly modify:

Database

Registry

Configuration

Internal Memory

Private Services

Plugins must run inside controlled boundaries.

---

# Update Rules

The update system must support:

Incremental Update

Rollback

Integrity Verification

Digital Signature Verification

Automatic Retry

Background Download

Download Resume

User Notification

Unsafe updates are prohibited.

---

# Installer Rules

The installer must support:

Express Installation

Custom Installation

Installation Path Selection

Desktop Shortcut

Start Menu Shortcut

Auto Update Option

Launch After Install

Repair

Modify

Uninstall

Installation must never force the system drive.

Users must always be able to choose installation location.

---

# Batch Operation Rules

Every list-based module must support:

Select All

Deselect All

Invert Selection

Tri-state Checkbox

Batch Delete

Batch Restore

Batch Export

Batch Ignore

Batch Enable

Batch Disable

Batch Analysis

Batch operations must remain responsive even when processing tens of thousands of items.

UI virtualization is mandatory.

---

# Startup Manager Rules

Startup items must never be displayed as one mixed list.

They must be automatically classified into:

Hardware Drivers

User Applications

Unknown

Critical Microsoft startup items must be protected.

Unsafe disable operations must require explicit confirmation.

AI recommendations must explain the impact of each startup item.

---

# UI Rules

All pages must follow the same Design System.

No inconsistent:

Buttons

Spacing

Radius

Typography

Colors

Animation

Icons

Interaction

UI consistency is mandatory.

---

---

# Source Control Rules

The project must use Git as the only source control system.

Branch strategy:

main

↓

develop

↓

feature/*

↓

release/*

↓

hotfix/*

Direct commits to the main branch are prohibited.

Every feature must be developed in an independent feature branch.

Every Pull Request must pass review before merging.

---

# Commit Message Rules

Commit messages must follow a unified convention.

Format:

type(scope): summary

Examples:

feat(cleaner): add browser cache scanner

fix(registry): improve invalid key detection

refactor(startup): optimize startup classification

perf(scan): reduce disk io

docs(ui): update design specification

test(core): add unit tests

chore(build): update dependencies

Allowed types:

feat

fix

perf

refactor

docs

test

build

ci

style

chore

Meaningless commit messages are prohibited.

Examples:

update

modify

test

aaa

123

tmp

---

# Pull Request Rules

Every Pull Request must include:

Purpose

Requirement

Architecture Impact

Risk Analysis

Testing Result

Performance Result

Memory Result

Screenshots (UI changes)

Rollback Strategy

Reviewer Checklist

No Pull Request may be merged without review.

---

# AI Development Workflow

For every new feature, AI must execute the following workflow.

Step 1

Requirement Analysis

↓

Step 2

Business Analysis

↓

Step 3

Architecture Design

↓

Step 4

Data Model Design

↓

Step 5

Interface Design

↓

Step 6

State Analysis

↓

Step 7

Implementation

↓

Step 8

Unit Testing

↓

Step 9

Integration Testing

↓

Step 10

Performance Benchmark

↓

Step 11

Memory Inspection

↓

Step 12

Security Inspection

↓

Step 13

Code Review

↓

Step 14

Architecture Review

↓

Step 15

Documentation

↓

Step 16

Release

Skipping any step is prohibited.

---

# Feature Completion Rules

A feature is considered complete only when all of the following are finished.

Business logic implemented

UI completed

Exception handling completed

Logging completed

Configuration completed

Localization completed

Unit tests completed

Integration tests completed

Performance verified

Memory verified

Security verified

Documentation completed

Release checklist passed

Anything less is considered incomplete.

---

# Code Review Rules

Every module must undergo a mandatory code review.

Review items include:

Architecture

Design Pattern

Naming

Code Style

Exception Handling

Dependency Direction

Dependency Injection

Performance

Memory

Security

Readability

Maintainability

Scalability

Thread Safety

Resource Release

Async Usage

Logging

Configuration

Documentation

Unit Test Coverage

Only reviewed code may enter the release branch.

---

# Performance Rules

Every module must define measurable performance goals.

Metrics include:

Execution Time

Memory Allocation

CPU Usage

Disk IO

GPU Usage

Network Usage

Thread Count

Database Query Time

Cache Hit Rate

Cold Start

Warm Start

Performance must be measured, not estimated.

---

# Memory Rules

Memory usage must remain stable during long-running sessions.

Requirements:

No memory leaks

No unmanaged resource leaks

No excessive allocations

Avoid LOH fragmentation

Reuse objects where appropriate

Dispose resources immediately after use

Release event subscriptions

Release timers

Release CancellationTokenSource

Release unmanaged handles

Support continuous execution without increasing memory usage over time.

---

# Threading Rules

Long-running work must execute on background threads.

UI updates must execute only on the UI thread.

Avoid unnecessary locks.

Prefer lock-free or low-contention algorithms.

Avoid deadlocks.

Avoid thread starvation.

Avoid busy waiting.

Support cancellation for all long-running tasks.

---

# Exception Handling Rules

Every exception must be:

Captured

Logged

Categorized

Displayed appropriately

Recoverable when possible

Never expose raw exception messages directly to end users.

Provide friendly explanations and recommended actions.

---

# Logging Rules

Logging must be structured.

Each log entry should include:

Timestamp

Module

Operation

Duration

Severity

Operation ID

Thread ID

Memory Usage

CPU Usage

Result

Exception

Logs should support filtering and searching.

---

# Security Rules

Every potentially destructive operation must support:

Confirmation

Backup

Rollback

Audit

Verification

Administrator privileges must only be requested when absolutely necessary.

Never execute elevated operations without user awareness.

---

# Testing Rules

Every module must include:

Unit Tests

Integration Tests

Regression Tests

Performance Tests

Stress Tests

Edge Case Tests

Error Recovery Tests

UI Tests (where applicable)

Testing is part of development, not a separate phase.

---

# Documentation Rules

Every module must include documentation for:

Purpose

Responsibilities

Architecture

Workflow

Dependencies

Configuration

Public APIs

Events

Error Handling

Performance Notes

Security Notes

Testing Strategy

Future Extension

Documentation must remain synchronized with implementation.

---

# Continuous Improvement Rules

Every optimization should be measurable.

Do not optimize based on assumptions.

Before optimization:

Measure

Analyze

Benchmark

After optimization:

Measure Again

Compare

Document Improvement

If no measurable improvement exists, the optimization should be reconsidered.

---
---

# Release Management Rules

Every released version of LeslieCleaner must satisfy commercial software quality standards.

A Release Build is the only build allowed to be distributed to end users.

The following build types are allowed internally:

Development

Testing

Preview

Release Candidate (RC)

Release

Only the Release version may be published publicly.

---

# Build Configuration Rules

Release builds must satisfy all of the following requirements:

Compilation Mode

Release

Optimization

Enabled

Debug Symbols

Disabled (or distributed separately)

Trace

Disabled

Developer Tools

Disabled

Experimental Features

Disabled

Mock Data

Removed

Test Data

Removed

Sample Data

Removed

Placeholder Pages

Removed

Unused Resources

Removed

Unused Assemblies

Removed

Unused Packages

Removed

Temporary Scripts

Removed

Backup Files

Removed

Cache Files

Removed

Generated Intermediate Files

Removed

No development artifacts may exist in the release package.

---

# Package Structure Rules

The published application should contain only the minimum required files.

The installer package should include:

Executable Files

Required Runtime

Required Native Libraries

Application Resources

Localization Resources

Configuration Templates

License Files

Privacy Policy

EULA

Update Metadata

No unrelated files should exist inside the installation directory.

---

# Installer Verification Checklist

Before publishing:

Verify installation succeeds.

Verify custom installation path works.

Verify installation without administrator permission (when applicable).

Verify installation with administrator permission.

Verify repair installation.

Verify upgrade installation.

Verify downgrade protection.

Verify uninstall.

Verify complete cleanup.

Verify user configuration preservation.

Verify automatic update.

Verify rollback after failed update.

---

# Upgrade Rules

Every version upgrade must support:

Configuration Migration

Database Migration

Rule Database Migration

Rollback

Backward Compatibility (where possible)

Existing user data must never be lost during upgrades.

---

# Uninstall Rules

Uninstalling LeslieCleaner should provide two modes:

Standard Uninstall

Remove Program Files

Preserve:

Configuration

History

Backups

Logs (optional)

User Preferences

Complete Uninstall

Remove everything.

Ask user for confirmation.

Display exactly what will be removed.

The uninstall process must never leave orphaned services, scheduled tasks, startup entries, registry keys or unnecessary files.

---

# Project Acceptance Criteria

The project is considered complete only if all of the following conditions are met.

Architecture

Complete

Business Logic

Complete

UI

Complete

Localization

Complete

Logging

Complete

Performance

Verified

Memory

Verified

Security

Verified

Testing

Passed

Documentation

Complete

Installer

Complete

Updater

Complete

Release Package

Verified

No Critical Bug

No Memory Leak

No Resource Leak

No Crash

No High Severity Warning

No Architecture Violation

Only then can the project be considered Production Ready.

---

# AI Forbidden Behaviors

The AI participating in this project must never:

Generate placeholder implementations.

Generate pseudo code instead of production code.

Ignore project architecture.

Ignore dependency direction.

Ignore performance requirements.

Ignore memory requirements.

Ignore thread safety.

Ignore exception handling.

Ignore logging.

Ignore rollback.

Ignore backup strategy.

Ignore user confirmation for dangerous operations.

Ignore localization.

Ignore accessibility.

Ignore high DPI compatibility.

Ignore future scalability.

Ignore plugin compatibility.

Ignore coding standards.

Ignore testing requirements.

Ignore documentation.

Ignore code review.

Ignore release review.

Ignore security review.

Never reduce implementation quality merely to shorten code.

Never sacrifice maintainability for brevity.

Never simplify business logic unless explicitly instructed.

---

# Commercial Software Principles

Every feature should satisfy the following characteristics.

Reliable

Stable

Recoverable

Observable

Maintainable

Extensible

Configurable

Measurable

Documented

Testable

Secure

Performant

Consistent

Professional

If any of these characteristics are missing, the feature should not be considered finished.

---

# AI Self-Validation Before Every Task

Before writing any code, AI must ask internally:

Have all requirements been understood?

Is the architecture appropriate?

Is the implementation scalable?

Is performance acceptable?

Can memory usage be improved?

Is the UI consistent?

Can the feature be tested?

Can failures be recovered?

Can users understand this feature?

Can developers maintain this feature?

If any answer is "No",

stop implementation,

improve the design first.

---

# LeslieCleaner Golden Rules

Rule 1

Safety is always more important than cleaning.

Rule 2

Architecture is always more important than implementation speed.

Rule 3

Maintainability is always more important than clever code.

Rule 4

Performance is a core feature, not an afterthought.

Rule 5

Memory optimization must be considered from the beginning.

Rule 6

Every destructive operation must support backup and rollback.

Rule 7

Every module must remain independently maintainable.

Rule 8

The UI must remain consistent across the entire application.

Rule 9

Business logic must never leak into the presentation layer.

Rule 10

Every release must meet commercial software quality standards.

---

# Definition of Done (DoD)

A task is considered "Done" only when:

✓ Requirements completed

✓ Architecture reviewed

✓ Code implemented

✓ Unit tests passed

✓ Integration tests passed

✓ Performance benchmark passed

✓ Memory benchmark passed

✓ Security review passed

✓ Code review passed

✓ Documentation completed

✓ Localization completed

✓ UI reviewed

✓ Accessibility reviewed

✓ High DPI verified

✓ Logging verified

✓ Rollback verified

✓ Installer verified (if applicable)

✓ Release checklist completed

Anything less than the above is considered "Work In Progress".

---

# Final Statement

This document establishes the mandatory development rules governing the LeslieCleaner project.

Every future document, module, feature specification, implementation plan and AI-generated output must comply with these rules.

Together with:

00_Master_System_Prompt.md

this document forms the highest-level governance of the LeslieCleaner Development Bible.

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

