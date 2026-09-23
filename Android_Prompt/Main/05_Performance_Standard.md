# LeslieCleaner Android Development Bible - 05_Performance_Standard

> Android Prompt ID: AP-MAIN-05-PERFORMANCE-STANDARD-MD  
> Source Prompt: `Main\05_Performance_Standard.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-05-PERFORMANCE-STANDARD-MD` in the Android traceability matrix.
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

Performance Standard

Priority

★★★★★

This document defines mandatory performance requirements for LeslieCleaner.

Performance is considered one of the primary competitive advantages of LeslieCleaner.

The software must not only provide more functions than traditional cleaners, but also maintain faster response speed, lower resource consumption and better user experience.

---

# Performance Philosophy

LeslieCleaner follows these principles:

Fast Response

↓

Low Resource Usage

↓

Predictable Performance

↓

Adaptive Resource Management

↓

Continuous Optimization

Performance improvements must be measurable.

No optimization should be accepted based only on assumptions.

---

# Performance Goals

The application should achieve:

Fast Startup

↓

Fast Scan

↓

Fast Analysis

↓

Fast Cleaning

↓

Fast Uninstall Residual Detection

↓

Fast UI Response

↓

Stable Long-term Running

---

# Core Performance Targets

## Application Startup

Cold Start:

Target:

≤ 3 seconds

Acceptable:

≤ 5 seconds


Warm Start:

Target:

≤ 1.5 seconds


Startup optimization priorities:

Minimal initialization

↓

Lazy Loading

↓

Async Service Loading

↓

Resource Preloading

↓

Cache Utilization

---

# UI Response Standard

All user interactions must feel instant.

Requirements:

Button Response:

≤100ms


Page Switching:

≤300ms


Animation:

60 FPS target


Search Input Response:

≤100ms


UI freezing is prohibited.

---

# Background Task Rules

Long operations must execute asynchronously.

Examples:

Disk Scan

↓

Registry Scan

↓

Application Analysis

↓

Hash Calculation

↓

Database Migration

↓

AI Analysis

---

The UI must always remain available.

---

# CPU Usage Standards

## Idle State

Target:

<1%

Acceptable:

<3%


---

## Normal Scan

Target:

10%~40%

---

## Deep Scan

Maximum:

80%

for short periods.

---

Continuous 100% CPU usage is prohibited.

---

# Adaptive CPU Management

The application must monitor:

CPU Load

↓

Battery Status

↓

User Activity

↓

Foreground Applications

---

When the user is active:

Reduce background priority.

When the system is idle:

Increase scanning throughput.

---

# Memory Usage Standards

Memory is a critical metric.

## Idle Memory

Target:

≤120MB


Acceptable:

≤200MB


---

## Normal Scan

Target:

≤350MB


---

## Deep Scan

Maximum:

≤600MB


---

# Memory Stability Requirement

During long-running operation:

Memory usage must not continuously increase.

Example:

Wrong:

Start:

200MB

After 1 hour:

1GB


Correct:

Start:

200MB

After 1 hour:

220MB

---

# Memory Monitoring

The application should monitor:

Heap Usage

↓

GC Frequency

↓

Object Count

↓

Large Object Heap

↓

Cache Size

↓

Thread Memory

---

# Memory Leak Prevention

Common leak sources:

Event Subscription

↓

Timer

↓

Thread

↓

File Handle

↓

Database Connection

↓

Native Resource

---

Every resource must have:

Creation

↓

Ownership

↓

Release

---

# Disk IO Performance

Disk operations are one of the most important bottlenecks.

Rules:

Avoid unnecessary disk access.

Avoid repeated scanning.

Avoid duplicate reading.

Use caching.

Use indexing.

---

# File Scan Optimization

Traditional approach:

Recursive Directory Traversal

↓

Read Every File

↓

Analyze Every File


This is slow.

---

LeslieCleaner preferred approach:

Application Metadata

↓

Known Locations

↓

Index Database

↓

USN Journal

↓

Targeted Scan

↓

Deep Scan Only When Required

---

# Scan Priority Model

Scanning should have multiple levels.

## Quick Scan

Purpose:

Daily maintenance


Targets:

Temporary Files

Cache

Recycle Bin

Common Junk


Expected Time:

<30 seconds

---

## Smart Scan

Purpose:

Recommended default mode


Targets:

Cleaner

Registry

Browser

Residual

Startup

Privacy


Expected Time:

1~3 minutes

---

## Deep Scan

Purpose:

Advanced users


Targets:

Full residual analysis

Advanced file analysis

Unused data discovery


May take longer.

---

# Scan Progress Architecture

Every scan must provide:

Overall Progress

↓

Module Progress

↓

Current Operation

↓

Estimated Remaining Time

↓

Items Found

↓

Space Available

---

Never show fake progress.

Progress must reflect real execution state.

---

# Multi-thread Scanning Standard

Scanning should use parallel execution.

Architecture:

Coordinator

↓

Task Queue

↓

Worker Threads

↓

Result Aggregator

↓

UI Update

---

Workers:

File Scanner

↓

Registry Scanner

↓

Residual Scanner

↓

Browser Scanner

↓

Startup Scanner

↓

Privacy Scanner

---

# Thread Count Rules

Default:

CPU Logical Cores - 1

Maximum:

Configurable

---

Never create unlimited threads.

---

# Task Scheduling Priority

Priority:

Critical System Task

↓

User Requested Task

↓

Interactive Scan

↓

Normal Maintenance

↓

Background Optimization

---

User actions always have higher priority than background tasks.

---

# CleanMyPC Residual Uninstall Optimization Standard

## Objective

The uninstall residual cleaning system is one of LeslieCleaner's core competitive advantages.

Traditional cleaners often have slow residual detection because they:

Scan the entire disk

↓

Search every directory

↓

Compare every file

↓

Scan the whole registry

↓

Wait for timeout

↓

Generate results


This approach causes:

Long waiting time

↓

High disk usage

↓

High CPU usage

↓

Poor user experience


LeslieCleaner must use a high-performance residual analysis architecture.

---

# Residual Scanner Architecture

The residual cleaning engine follows:

Application Information Collection

↓

Installation Metadata Database

↓

Fast Candidate Detection

↓

Multi-source Analysis

↓

Confidence Calculation

↓

Risk Evaluation

↓

User Confirmation

↓

Cleanup Execution

↓

Verification

---

# Residual Detection Strategy

Residual detection must not rely on full disk scanning by default.

Detection sources:

## 1. Installation Metadata

Collect during installation:

Application Name

↓

Publisher

↓

Installation Path

↓

Executable Files

↓

Registry Entries

↓

Services

↓

Startup Items

↓

Scheduled Tasks

↓

Browser Extensions

↓

File Associations


Store metadata locally.

---

## 2. Uninstall Monitoring

Before uninstall:

Create Snapshot:

Registry State

↓

File Structure

↓

Services

↓

Startup Entries


After uninstall:

Compare:

Before

↓

After


Generate:

Residual Difference Report

---

## 3. Smart Path Prediction

Use application patterns.

Examples:

Common locations:

```
C:\Program Files\
C:\Program Files (x86)\
C:\Users\%USERNAME%\AppData\
C:\ProgramData\
```

Search priority:

High probability paths

↓

Related folders

↓

Cache locations

↓

Configuration locations


Avoid scanning unrelated directories.

---

# Residual Detection Database

Create:

ResidualIndex Database

Purpose:

Store application fingerprints.

Structure:

ApplicationId

↓

Publisher

↓

File Patterns

↓

Registry Patterns

↓

Folder Patterns

↓

Service Patterns

↓

Startup Patterns

↓

Confidence Score


---

# Application Fingerprint System

Each application should have:

Unique Identifier

↓

File Signature

↓

Registry Signature

↓

Path Signature

↓

Publisher Signature


Example:

Chrome:

Executable:

chrome.exe


Folders:

Google\Chrome


Registry:

Google Chrome


---

# Incremental Residual Scan

Do not rebuild all indexes every time.

Use:

Incremental Update

↓

Changed Files Only

↓

Modified Registry Only

↓

New Applications Only


---

# USN Journal Optimization

  NTFS USN Journal should be used when available.

Purpose:

Track filesystem changes.

Advantages:

No full disk traversal.

Faster change detection.

Lower disk IO.


Usage:

Monitor:

Created Files

↓

Deleted Files

↓

Modified Files


---

# File Hash Optimization

Hash calculation is expensive.

Do not immediately calculate full hash.

Use multi-stage verification.

---

## Stage 1

File Name

↓

Extension

↓

Size


Fast filtering.

---

## Stage 2

Partial Hash

Calculate:

Beginning Block

↓

Middle Block

↓

Ending Block


---

## Stage 3

Full Hash

Only for suspicious matches.

---

# Hash Algorithm Selection

Preferred:

SHA-256

For security verification.


For duplicate detection:

Fast non-security hash:

xxHash

↓

MurmurHash


Then:

SHA-256 confirmation.

---

# Registry Cleaning Performance

Registry scanning must not enumerate everything blindly.

---

# Registry Scan Priority

High Priority:

Uninstalled Application Keys

↓

Invalid Startup Entries

↓

Missing File References

↓

Broken Associations


Medium Priority:

Unused Extensions

↓

Old Settings


Low Priority:

Historical Entries


---

# Registry Cache

Maintain:

Registry Analysis Cache


Store:

Previously analyzed keys

↓

Risk result

↓

Modification time


If unchanged:

Reuse result.

---

# Registry Backup Optimization

Before modification:

Only backup affected keys.

Avoid:

Export entire registry every time.


---

# Batch Operation Performance

Batch operations are common.

Examples:

Batch uninstall

↓

Batch delete

↓

Batch startup management

↓

Batch cleanup


---

# Batch Processing Architecture

User Selection

↓

Operation Queue

↓

Worker Pool

↓

Progress Aggregator

↓

Result Collector

↓

History Storage


---

# Select All Optimization

Every batch module must provide:

Select All

↓

Select None

↓

Invert Selection

↓

Filter Selection


---

# Batch Safety Rules

Before batch execution:

Show:

Selected Count

↓

Estimated Time

↓

Risk Level

↓

Required Permission


---

# Batch Execution

Must support:

Pause

↓

Resume

↓

Cancel

↓

Retry Failed Items


---

# Database Performance Standard

LeslieCleaner uses local database.

Recommended:

SQLite

---

# Database Optimization

Required:

Indexes

↓

Prepared Statements

↓

Transactions

↓

Connection Pooling

↓

Async Queries


---

# Database Tables Optimization

Large tables:

History

↓

Scan Results

↓

File Index

↓

Application Metadata


must support:

Pagination

↓

Lazy Loading

↓

Archive


---

# Database Maintenance

Automatic:

Integrity Check

↓

Vacuum

↓

Index Optimization

↓

Cache Cleanup


---

# UI Virtualization Performance

Large lists must use virtualization.

Examples:

Installed Applications

↓

Large Files

↓

Registry Items

↓

Startup Items

↓

Scan Results


---

# Virtual List Requirements

Support:

Thousands Items

↓

Smooth Scrolling

↓

Lazy Rendering

↓

Incremental Loading


---

# UI Data Loading

Never:

Load all data before displaying.

---

Use:

Initial Small Batch

↓

Display Immediately

↓

Background Loading

↓

Append Results

---

# Progress Update Optimization

Do not update UI every item.

Bad:

Update UI:

Every File

---

Better:

Batch update:

Every 100~500 items

or

Every 100ms


---

# Notification Performance

Notifications should:

Not block operations.

Not interrupt workflow.

Use:

Queue

↓

Priority

↓

Expiration


---

# Cache Strategy

Cache types:

Application Cache

↓

Scan Cache

↓

Rule Cache

↓

UI Cache

↓

Metadata Cache


---

# Cache Rules

Cache must have:

Expiration

↓

Size Limit

↓

Invalidation Strategy

↓

Cleanup Policy


---

# Performance Monitoring

The application must collect:

Startup Time

↓

Scan Duration

↓

CPU Usage

↓

Memory Usage

↓

Disk IO

↓

Database Time

↓

UI Response


---

# Performance Regression Prevention

Every release must compare:

Previous Version

VS

Current Version


Monitor:

Speed

↓

Memory

↓

CPU

↓

Disk Usage


Performance degradation requires review.

---
---

# Application Startup Optimization Standard

Startup speed directly affects user perception.

LeslieCleaner must prioritize startup performance.

---

# Startup Loading Architecture

Traditional:

Application Start

↓

Load Everything

↓

Initialize Services

↓

Load Database

↓

Load Rules

↓

Show UI


Problem:

Slow startup.

Large memory occupation.

---

LeslieCleaner startup architecture:

Application Start

↓

Minimal Runtime Initialization

↓

Display Main UI

↓

Background Service Loading

↓

Lazy Module Initialization

↓

Ready State


---

# Startup Phase Definition

## Phase 1: Bootstrap

Target:

<500ms


Responsibilities:

Process initialization

↓

Configuration loading

↓

Dependency container creation

↓

Crash protection initialization


---

## Phase 2: UI Ready

Target:

≤3 seconds


Responsibilities:

Main window display

↓

Navigation initialization

↓

Basic service availability


---

## Phase 3: Background Initialization

No blocking.

Load:

Scanner Engine

↓

Rule Database

↓

AI Service

↓

Statistics

↓

Cache


---

# Lazy Loading Rules

Modules should initialize only when needed.

Example:

User opens:

Registry Cleaner

↓

Load Registry Module


Do not load:

Registry Engine

at application startup.

---

# Resource Preloading Strategy

Preload:

Application Logo

↓

Main UI Resources

↓

Common Icons


Delay:

Large Images

↓

Charts

↓

Detailed Rules

↓

AI Models


---

# Memory Optimization Architecture

## Memory Allocation Principle

Prefer:

Reuse

↓

Stream

↓

Release

↓

Cache Carefully


Avoid:

Allocate

↓

Copy

↓

Discard


---

# Large Object Handling

Large objects include:

Large Files

↓

Scan Results

↓

Images

↓

Database Results


Rules:

Never keep unnecessary large objects alive.

Use:

Streaming

↓

Pagination

↓

Chunk Processing


---

# Scan Result Optimization

Do not store millions of objects directly.

Bad:

```
List<FileInfo>
```

containing millions of items.

---

Better:

Use:

Paged Result

↓

Lightweight Model

↓

Database Storage

↓

On-demand Loading


---

# String Memory Optimization

Avoid:

Repeated string creation.

Use:

String Pool

↓

StringBuilder

↓

Cached Values


---

# Object Pooling

Use object pools for frequently created objects.

Examples:

Scan Result Object

↓

Hash Buffer

↓

File Information Object

↓

Temporary Analysis Context


---

# Cache Memory Control

Every cache requires:

Maximum Size

↓

Expiration

↓

Removal Strategy


---

Example:

Scan Cache:

Maximum:

200MB


Expiration:

7 days


---

# Background Resource Scheduling

LeslieCleaner should adapt to system conditions.

Monitor:

CPU

↓

Memory

↓

Disk Activity

↓

Battery

↓

User Activity


---

# Intelligent Performance Mode

Provide:

Performance Mode

↓

Balanced Mode

↓

Battery Saving Mode


---

# Performance Mode

For desktop users.

Behavior:

Higher scan concurrency

↓

More aggressive caching

↓

Faster analysis


---

# Balanced Mode

Default.

Behavior:

Moderate CPU usage

↓

Normal scan speed

↓

Normal memory


---

# Battery Saving Mode

For laptops.

Behavior:

Lower thread count

↓

Reduced disk access

↓

Pause background tasks


---

# SSD Optimization

SSD characteristics:

Fast random access

↓

Lower seek cost


Optimization:

Parallel reads

↓

Higher concurrency

↓

Cache metadata


---

# HDD Optimization

HDD characteristics:

Slow random access

↓

Seek cost high


Optimization:

Sequential reading

↓

Reduced parallelism

↓

Batch directory access


---

# Storage Detection

Detect:

SSD

↓

HDD

↓

External Drive


Adjust:

Thread count

↓

Scan strategy

↓

Cache policy


---

# Large File Processing Standard

Large files:

>500MB


Very large:

>5GB


---

# Large File Analysis

Never:

Load entire file.


Use:

Metadata Analysis

↓

Chunk Reading

↓

Streaming Hash

↓

Partial Analysis


---

# Disk Space Analysis Optimization

Do not scan repeatedly.

Use:

Filesystem Index

↓

Cached Statistics

↓

Incremental Update


---

# Duplicate Detection Performance

Process:

Size Grouping

↓

Extension Grouping

↓

Partial Hash

↓

Full Hash


Avoid:

Hashing every file immediately.


---

# AI Module Performance

AI functions must never affect core performance.

AI runs:

Asynchronous

↓

Optional

↓

Interruptible


---

# AI Processing Rules

AI tasks:

Must have timeout.

Must support cancellation.

Must limit memory.

Must not block cleaning operations.


---

# Plugin Performance Isolation

Plugins run separately.

Rules:

Plugin timeout

↓

Memory limit

↓

Exception isolation

↓

Crash protection


---

# Performance Testing Standard

Every major version requires:

Startup Test

↓

Scan Test

↓

Memory Test

↓

CPU Test

↓

IO Test

↓

Stress Test


---

# Performance Test Environment

Record:

CPU Model

↓

Memory Size

↓

Disk Type

↓

  Version

↓

Application Version


---

# Required Benchmark Cases

## Startup Benchmark

Measure:

Cold Start

Warm Start

UI Ready Time


---

## Cleaner Benchmark

Test:

100GB Temporary Data

↓

1 Million Files

↓

Large Cache


---

## Registry Benchmark

Test:

Large Registry Dataset

↓

Thousands Invalid Entries


---

## Uninstaller Benchmark

Test:

Small Application

↓

Large Application

↓

Complex Residual


---

## Memory Benchmark

Run:

8 Hours Continuous Operation


Check:

Memory Growth

↓

Handle Count

↓

Thread Count


---

# Performance Acceptance Criteria

Release accepted only when:

Startup:

≤3 seconds


Idle Memory:

≤120MB


No Memory Leak:

8 hours stable


UI:

No freezing


Scan:

Performance regression <10%


CPU:

No uncontrolled usage


---

# Performance Optimization Priority

When optimization is required:

Priority 1:

User Experience


Priority 2:

Startup Speed


Priority 3:

Memory Usage


Priority 4:

Scan Speed


Priority 5:

CPU Efficiency


Priority 6:

Disk IO


---

# Final Performance Statement

LeslieCleaner must not become another resource-heavy cleaning application.

The goal is:

Faster than traditional cleaners

↓

Lower memory usage

↓

Smarter scanning

↓

More accurate detection

↓

Better user experience

Performance is a core product capability.

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

