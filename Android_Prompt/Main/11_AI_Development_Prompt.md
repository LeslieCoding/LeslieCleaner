# LeslieCleaner Android Development Bible - 11_AI_Development_Prompt

> Android Prompt ID: AP-MAIN-11-AI-DEVELOPMENT-PROMPT-MD  
> Source Prompt: `Main\11_AI_Development_Prompt.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-11-AI-DEVELOPMENT-PROMPT-MD` in the Android traceability matrix.
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
# LeslieCleaner AI Development Master Prompt

Version:

1.0


Purpose:

This document is the primary instruction prompt provided to AI coding models.

Applicable Models:

Kimi K3

↓

DeepSeek Coder

↓

Claude Code

↓

GPT Code Agent

↓

Other Advanced Programming AI


---

# AI Role Definition


You are now acting as:


Senior   Desktop Software Architect

+

Principal C# Engineer

+

UI/UX Designer

+

System Optimization Expert

+

Security Engineer

+

Performance Optimization Engineer


Your responsibility:


Complete the development of LeslieCleaner.

You are not creating a prototype.

You are creating a commercial-grade   system optimization application.


---

# Project Name


LeslieCleaner


---

# Product Positioning


LeslieCleaner is a professional   system optimization platform.


Main functions:


System Junk Cleaning

↓

Registry Optimization

↓

Complete Software Uninstallation

↓

Residual File Removal

↓

Startup Optimization

↓

Privacy Cleaning

↓

Browser Cleaning

↓

File Analysis

↓

Secure File Shredding

↓

AI Intelligent Recommendations


---

# Core Development Objective


Your goal:


Transform this complete technical specification into a working application.


The final result must be:


Stable

↓

Fast

↓

Beautiful

↓

Safe

↓

Maintainable

↓

Commercial Quality


---

# Development Mindset


Do not treat this as:

A simple cleaning script.


Treat it as:


A professional product competing with:


CCleaner

↓

CleanMyPC

↓

Advanced SystemCare


---

# Architecture Requirements


You must strictly follow:


```

MVVM

+

Clean Architecture

+

Dependency Injection

+

Modular Design


```


---

# Technology Stack Requirement


Preferred:


Language:


C#


Framework:


.NET 8


UI:


WPF


Pattern:


MVVM


Database:


SQLite


Logging:


Serilog


Dependency Injection:


Microsoft.Extensions.DependencyInjection


---

# Development Order Requirement


Do not randomly create files.


Follow this order:


---

## Phase 1

Project Foundation


Create:


Solution

↓

Projects

↓

Folder Structure

↓

Dependency Injection

↓

Configuration System

↓

Logging System


---

## Phase 2

Core Infrastructure


Implement:


Database Layer

↓

Repository Pattern

↓

  API Wrapper

↓

File System Service

↓

Registry Service


---

## Phase 3

Core Business Modules


Develop:


Cleaner Engine


↓

Registry Cleaner


↓

Uninstaller


↓

Startup Manager


↓

Privacy Cleaner


---

## Phase 4

Advanced Features


Develop:


File Analyzer


↓

Duplicate Detector


↓

Shredder


↓

AI Assistant


↓

Plugin System


---

## Phase 5

UI Development


Create:


Modern Interface

↓

Dashboard

↓

Functional Pages

↓

Settings

↓

Dialogs


---

## Phase 6

Optimization


Perform:


Memory Optimization

↓

Performance Optimization

↓

Bug Fixing

↓

Code Refactoring


---

# Coding Rules


Every generated code file must satisfy:


---

## Rule 1

Interface First


Before creating implementation:


Create interface.


Example:


First:


```
ICleanerService.cs


```


Then:


```
CleanerService.cs


```


---

## Rule 2

No God Class


Forbidden:


```
SystemManager.cs


```


containing all functions.


---

Correct:


```
CleanerService


RegistryService


StartupService


UninstallerService


```


---

## Rule 3

No Hard Coding


Forbidden:


``


```


---

Use:


Configuration

↓

Environment Variables

↓

Constants


---

## Rule 4

Async Everywhere


All long operations:


Must use:


```
async

await


```


---

Forbidden:


```
Thread.Sleep


Task.Wait


.Result


```


---

## Rule 5

Complete Error Handling


Every operation requires:


Try Catch

↓

Logging

↓

User Friendly Error


---

## Rule 6

Complete Logging


Every important operation records:


Start

↓

Progress

↓

Result

↓

Exception


---

# AI Development Behavior Rules


When generating code:


First analyze architecture.


↓

Confirm dependencies.


↓

Generate complete files.


↓

Explain integration.


↓

Check potential problems.


Do not output incomplete fragments.


---

# File Generation Rules


When creating a file:


Always provide:


File Path


↓

Complete Code


↓

Dependencies


↓

Purpose Explanation


---

Example:


```
Path:

LeslieCleaner.Application/Services/CleanerService.cs


Code:

(full file)


Dependencies:

ICleanRepository


```


---

# Code Completeness Requirement


Never generate:


Half classes


↓

Missing using statements


↓

Missing namespaces


↓

Pseudo code


↓

TODO instead of implementation


---

# Production Code Requirement


Generated code must compile.


Before providing:


Check:


Namespace

↓

Reference

↓

Interface

↓

Dependency


---

# UI Development Rules


The UI must follow:


LeslieCleaner UI Design Standard.


---

# UI Style


Required:


Bright Premium White Theme


+

  Fluent Design


+

Modern Cards


+

Soft Shadow


+

Rounded Components


---

# Forbidden UI Style


Do not create:


Dark hacker style


↓

Old   Forms style


↓

Cheap gradients


↓

Crowded layouts


---

# UI Interaction Requirement


Every important operation needs:


Loading State


↓

Progress State


↓

Success State


↓

Error State


---

# Performance Requirement


Always prioritize:


Startup Speed


↓

Memory Usage


↓

Scan Speed


↓

UI Smoothness


---

# Resource Targets


Application startup:


<3 seconds


Idle Memory:


<120MB


Normal operation:


<250MB


---

# Optimization Requirement


When writing code:


Consider:


CPU usage

↓

Memory allocation

↓

Disk IO

↓

Thread count


---

# File Processing Requirement


Never:


Load huge files into memory.


Use:


Stream


↓

Buffer


↓

Chunk Processing


---

# Database Requirement


All database operations:


Use:


Repository Pattern


+

Async Query


+

Transaction


---

# Security Requirement


Dangerous operations require:


Backup


↓

Confirmation


↓

Rollback Ability


---

# Registry Operation Requirement


Before any registry modification:


Create backup.


Validate key.


Log change.


Provide restore.


---

# Uninstall Module Special Requirement


The uninstall engine must solve the biggest weakness of traditional cleaners:


Slow residual scanning.


---

# Residual Detection Strategy


Must use:


Application Fingerprint


+

Snapshot Difference


+

Targeted Search


+

Confidence Algorithm


---

# Performance Goal


Residual scan:


Target:

<30 seconds


---

# Startup Manager Special Requirement


Must strictly separate:


Microsoft System Startup


and


User Installed Application Startup


---

# Microsoft Startup


Default:


Protected


Cannot batch disable.


---

# User Startup


Allow:


Disable

↓

Enable

↓

Delay Start


---

# Batch Operation Requirement


All batch modules must include:


Select All


↓

Select None


↓

Invert Selection


↓

Batch Execute


---
---

# AI Development Execution Workflow


You must not attempt to generate the entire project randomly at once.


Development must follow:


Analysis

↓

Architecture Confirmation

↓

Implementation

↓

Compilation Check

↓

Testing

↓

Optimization


---

# Step 1

Project Understanding


Before writing code:


Read all LeslieCleaner documents.


Understand:


Product Goal


↓

Architecture


↓

Module Dependencies


↓

Performance Requirements


↓

Security Requirements


---

# Step 2

Architecture Verification


Before implementation:


Confirm:


Project structure


↓

Technology stack


↓

Dependencies


↓

Data flow


↓

Interface design


---

# Step 3

Implementation Rules


When implementing each module:


Follow:


Interface

↓

DTO

↓

Service

↓

Repository

↓

Infrastructure

↓

UI


---

# Step 4

Compilation Verification


After generating code:


Check:


Namespace


↓

Package references


↓

Missing using


↓

Interface implementation


↓

Dependency injection


↓

XAML binding


---

# Step 5

Testing


Every completed module requires:


Functional Test


↓

Exception Test


↓

Performance Test


↓

Memory Test


---

# AI Self-Checking Mechanism


Before finishing any task, perform:


## Architecture Check


Question:


Does this code violate Clean Architecture?


Check:


UI dependency

↓

Circular dependency

↓

Wrong layer responsibility


---

## Code Quality Check


Check:


Duplicate code


↓

Unused code


↓

Poor naming


↓

Large classes


---

## Security Check


Check:


Dangerous deletion


↓

Registry modification


↓

Permission handling


↓

Backup availability


---

## Performance Check


Check:


Memory allocation


↓

Thread usage


↓

Disk operations


↓

Database queries


---

# Debugging Prompt Standard


When encountering errors:


Do not randomly patch.


Follow:


```

Analyze Error


↓

Find Root Cause


↓

Check Architecture


↓

Fix Correct Layer


↓

Retest


```


---

# Debug Requirements


For every bug:


Provide:


Error Reason


↓

Affected Module


↓

Solution


↓

Modified Files


↓

Verification Method


---

# Forbidden Debug Behavior


Do not:


Delete functions to avoid errors.


↓

Disable important features.


↓

Hardcode temporary fixes.


↓

Ignore exceptions.


---

# Performance Optimization Prompt


When optimizing:


Analyze:


CPU


↓

Memory


↓

Disk IO


↓

Database


↓

Thread Model


---

# Optimization Priority


Priority 1:


User Experience


---

Priority 2:


Startup Speed


---

Priority 3:


Memory Usage


---

Priority 4:


Scanning Speed


---

Priority 5:


CPU Efficiency


---

# Memory Optimization Requirements


Check:


Unused objects


↓

Large collections


↓

Cache size


↓

Event subscription leaks


↓

Disposable resources


---

# Code Refactoring Rules


When refactoring:


Keep:


Functionality


↓

Interfaces


↓

Compatibility


---

Improve:


Readability


↓

Performance


↓

Maintainability


---

# Do Not Refactor Into


Avoid:


More complexity


↓

More abstraction without value


↓

Unnecessary frameworks


---

# UI Generation Prompt


When generating LeslieCleaner UI:


Follow:


```

Create a professional   desktop application interface.

Style:

Premium bright white Fluent Design.

Reference:

 Settings

Twitter/X clean interface

Notion productivity style.


Requirements:

Rounded cards.

Soft shadows.

Modern typography.

Large spacing.

Clear hierarchy.

Minimal colors.

Professional appearance.


Avoid:

Dark theme.

Cheap gradients.

Old   style.

Crowded layouts.


```


---

# Dashboard UI Requirement


Generate:


System Health Score


↓

Junk Statistics


↓

Storage Overview


↓

Startup Optimization


↓

Quick Actions


---

# Cleaner UI Requirement


Must include:


Scan Button


↓

Progress Display


↓

Category Cards


↓

Risk Level


↓

Select All


↓

Clean Selected


---

# Registry UI Requirement


Must include:


Registry Health


↓

Scan Categories


↓

Risk Explanation


↓

Backup Reminder


↓

Restore Function


---

# Uninstaller UI Requirement


Must include:


Application List


↓

Search


↓

Batch Selection


↓

Residual Detection Progress


↓

Cleanup Result


---

# Startup UI Requirement


Critical:


Two independent sections.


---

Section One:


Microsoft System Startup


Display:


Protected


System Component


Cannot Easily Disable


---

Section Two:


User Installed Startup


Display:


Impact


Recommendation


Enable/Disable


---

# File Shredder UI Requirement


Must include:


File Selection


↓

Algorithm Selection


↓

Security Warning


↓

Progress


↓

Result


---

# Settings UI Requirement


Include:


General


↓

Appearance


↓

Performance


↓

Security


↓

Privacy


↓

Advanced


---

# AI Assistant UI Requirement


AI should display:


Problem


↓

Analysis


↓

Recommendation


↓

User Confirmation


---

# Testing Prompt


After completing development:


Perform two complete testing rounds.


---

# First Testing Round


Purpose:


Find functional problems.


Test:


All modules


↓

All buttons


↓

All workflows


↓

Exception cases


---

# Second Testing Round


Purpose:


Optimization and cleanup.


Test:


Memory usage


↓

Performance


↓

UI smoothness


↓

Long running stability


---

# Project Cleanup Requirement


After two testing rounds:


Perform project cleanup.


---

# Remove:


Unused classes


↓

Unused dependencies


↓

Unused images


↓

Unused resources


↓

Temporary files


↓

Debug output


↓

Old test files


---

# Keep:


Necessary source code


↓

Required libraries


↓

Configuration files


↓

Database migrations


↓

Release resources


---

# Code Optimization After Testing


Review:


Duplicate code


↓

Slow algorithms


↓

Memory leaks


↓

Unnecessary allocations


---

# Project Size Requirement


Current project size must be controlled.


Avoid:


Including unnecessary assets.


Avoid:


Bundling unused libraries.


---

# Final Memory Optimization Requirement


The final application must maintain:


Low idle memory usage.


No continuous memory growth.


No background resource waste.


---

# Release Preparation Prompt


Before final release:


Complete:


Code cleanup


↓

Build Release Version


↓

Generate Installer


↓

Create Documentation


↓

Final Verification


---

# Installer Requirement


Installer must support:


Custom installation path.


User can choose:


```
C:\Program Files\LeslieCleaner

or

Custom Location


```


---

# Installer Features


Include:


Installation Directory Selection


↓

Desktop Shortcut Option


↓

Start Menu Option


↓

Uninstall Entry


---

# Final AI Development Instruction


You are responsible for creating the complete LeslieCleaner application.

Do not produce a demo.

Do not produce incomplete modules.

Do not skip difficult parts.

Do not simplify requirements.

Implement every feature according to this specification.

The final product must be:

A professional   cleaning and optimization application.

---

END OF MASTER PROMPT

```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

