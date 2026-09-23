# LeslieCleaner Android Development Bible - 06_Memory_Optimization

> Android Prompt ID: AP-MAIN-06-MEMORY-OPTIMIZATION-MD  
> Source Prompt: `Main\06_Memory_Optimization.md`  
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

- [ ] Add/confirm requirement IDs `AP-MAIN-06-MEMORY-OPTIMIZATION-MD` in the Android traceability matrix.
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

UI Design Standard

Priority

★★★★★

This document defines the complete user interface and user experience standards for LeslieCleaner.

All UI development, AI-generated interfaces, components and visual designs must comply with this document.

The goal is to create a premium-level   cleaning application interface.

---

# UI Design Philosophy

LeslieCleaner UI should communicate:

Professional

↓

Trustworthy

↓

Fast

↓

Modern

↓

Premium

↓

Simple

↓

Powerful

---

The interface should not look like:

Old   Utility Software

↓

Cheap Antivirus Software

↓

Overloaded System Tools

↓

Complex Enterprise Panels

↓

Advertising-driven Applications

---

# Visual Design Direction

Primary Style:

Modern Bright Premium Interface


Reference:

Microsoft Fluent Design

↓

Settings

↓

Twitter / X Web Interface

↓

Notion

↓

Raycast

↓

Arc Browser


---

# Overall Visual Tone

The interface should feel:

Clean white background

↓

Soft contrast

↓

High-quality spacing

↓

Professional typography

↓

Subtle shadows

↓

Smooth animation

↓

Premium materials


---

# Color System

## Primary Background

Use:

Premium Warm White


Characteristics:

Not pure white.

Avoid:

#FFFFFF everywhere.


Recommended feeling:

Soft

Comfortable

Elegant


---

# Surface Colors

Levels:

Background

↓

Card Surface

↓

Floating Panel

↓

Dialog Surface


Each layer should have slight contrast.

Example:

Background:

Warm White


Card:

Slight Gray White


Floating:

Pure White


---

# Accent Color

Accent color should represent:

Technology

↓

Safety

↓

Trust


Recommended:

Blue

↓

Blue Purple

↓

Modern Cyan


Avoid:

Aggressive Red

↓

Bright Green

↓

Cheap Gradient Colors


---

# Semantic Colors

Required:

Success

Warning

Error

Information


Examples:

Success:

Cleanup completed


Warning:

Risk item detected


Error:

Operation failed


Information:

Recommendation


---

# Typography System

Use modern UI fonts.

Preferred:

Segoe UI Variable

↓

Inter

↓

SF Pro style


---

# Font Hierarchy

## Main Title

Purpose:

Page title


Example:

"系统清理"


Large

Bold


---

## Section Title

Purpose:

Module title


Example:

"扫描结果"


Medium

Semi-bold


---

## Body Text

Purpose:

Descriptions

Normal weight.


---

## Secondary Text

Purpose:

Additional information


Use:

Lower contrast

Smaller size


---

# Spacing System

All UI spacing must follow unified scale.

Recommended:

4px

↓

8px

↓

12px

↓

16px

↓

24px

↓

32px

↓

48px


---

# Avoid

Random spacing.

Example:

13px

27px

39px


unless required.

---

# Corner Radius System

Use soft rounded design.

Components:

Small:

8px


Medium:

12px


Large:

16px


Cards:

16~20px


---

# Shadow System

Avoid heavy shadows.

Use:

Soft elevation


Levels:

Level 1:

Small card


Level 2:

Floating panel


Level 3:

Dialog


---

# Border System

Avoid excessive borders.

Prefer:

Shadow

↓

Background difference

↓

Spacing


Use borders only when necessary.

---

# Icon Design

Icons must be:

Simple

↓

Modern

↓

Consistent

↓

Recognizable


Preferred:

Fluent Icons

↓

Material Symbols


---

# Icon Rules

Every icon must have:

Meaning

↓

Tooltip

↓

Consistent size


---

# Icon Size Standard

Small:

16px


Normal:

20px


Large:

24px


Feature Icon:

32~48px


---

# Main Application Layout

Overall structure:

```
------------------------------------------------

Header

------------------------------------------------

Sidebar        Main Content


               Content Area


------------------------------------------------

Status Area

------------------------------------------------
```

---

# Header Design

Contains:

Application Logo

↓

Current Module Name

↓

Search

↓

Notification

↓

Settings

↓

User Area


---

# Sidebar Design

Purpose:

Main navigation.

Modules:

Dashboard

↓

Cleaner

↓

Registry

↓

Uninstaller

↓

Startup Manager

↓

Privacy

↓

File Analyzer

↓

Shredder

↓

Settings


---

# Sidebar Requirements

Support:

Expanded Mode

↓

Collapsed Mode


Animation:

Smooth transition


---

# Sidebar Item Design

Each item:

Icon

↓

Name

↓

Selected State


Selected state:

Accent background

↓

Rounded container

↓

Clear contrast


---

# Dashboard Design

Dashboard is the first impression.

Must include:

System Health

↓

Junk Statistics

↓

Storage Overview

↓

Startup Optimization

↓

Quick Actions


---

# Dashboard Cards

Each card should show:

Title

↓

Value

↓

Trend

↓

Action


Example:

Junk Files

12.5GB

Clean Now


---

# Dashboard Principles

Avoid:

Too many numbers

↓

Complex charts

↓

Technical overload


The user should understand system status within seconds.

---

# Animation Principles

Animation should be:

Fast

↓

Smooth

↓

Useful


Never:

Decorative only

↓

Slow

↓

Distracting


---

# Animation Duration

Micro interaction:

100ms


Normal transition:

200ms


Large transition:

300ms


---

# Loading Design

Never show:

Blank screen


Use:

Skeleton Loading

↓

Progress Animation

↓

Status Text


---

# Empty State Design

Every empty page requires:

Illustration

↓

Explanation

↓

Next Action


Example:

"No unnecessary files found"

"Your system is already clean."

---

# Error State Design

Error UI must provide:

What happened

↓

Why

↓

What user can do


---

# UI Performance Requirement

UI must support:

Thousands of records

↓

Smooth scrolling

↓

Async loading

↓

Virtualization


---

# UI Architecture Requirement

All UI follows:

MVVM

+

Component Design System

+

Reusable Components


No page should create independent styles.

---
---

# Cleaner Module UI Design

## Purpose

The Cleaner module is the core function of LeslieCleaner.

The interface must make users understand:

What can be cleaned

↓

How much space can be recovered

↓

What risks exist

↓

How to execute cleaning safely

---

# Cleaner Main Page Layout

Structure:

```
------------------------------------------------

Cleaning Overview

------------------------------------------------


          System Cleanliness Score


------------------------------------------------


 Junk Categories


 Temporary Files        5.2GB

 Browser Cache          2.8GB

 System Logs            1.1GB

 Application Cache      3.6GB


------------------------------------------------


          [ Start Smart Scan ]


------------------------------------------------
```

---

# Cleanliness Score Design

Display:

Large circular indicator.

Example:

```
System Health

92%

Excellent
```

---

Score calculation:

Junk Amount

↓

Startup Optimization

↓

Privacy Risk

↓

System Condition


---

# Scan Button Design

Primary action:

Start Smart Scan


Requirements:

Large

Clear

High visibility


States:

Idle

↓

Scanning

↓

Paused

↓

Completed

---

# Scan Progress UI

Must display:

Current Module

↓

Current Action

↓

Items Found

↓

Recovered Space

↓

Estimated Remaining Time


Example:

```
Scanning browser cache...

Found:

12500 files

Recovered:

3.2GB

Remaining:

20 seconds
```

---

# Cleaning Category Cards

Each category card contains:

Icon

↓

Name

↓

Size

↓

Risk Level

↓

Selection Checkbox


Example:

```
□   Temporary Files

3.5GB

Low Risk
```

---

# Risk Level Display

Three levels:

Safe

↓

Recommended

↓

Careful


Avoid technical terms users do not understand.

---

# User Selection Design

Every cleaning item supports:

Single Selection

↓

Multi Selection

↓

Select All

↓

Select None

↓

Invert Selection


---

# Batch Operation UI Requirement

All batch modules must contain:

```
[✓ Select All]


Selected:

125 Items


Total Size:

8.6GB


[ Clean Selected ]
```

---

# Registry Cleaner UI Design

## Purpose

Provide safe registry optimization.

Registry cleaning is high-risk.

UI must emphasize safety.

---

# Registry Main Page

Layout:

```
Registry Health

--------------------

Scan Categories


Invalid Paths

Unused Extensions

Startup Errors

Application Residues


--------------------

[ Scan Registry ]

```

---

# Registry Scan Result

Each item displays:

Registry Path

↓

Problem Type

↓

Risk Level

↓

Recommended Action


---

# Registry Risk Visualization

Use:

Safe color indicator

↓

Warning indicator

↓

High-risk indicator


Never encourage deleting everything.

---

# Registry Backup UI

Before cleaning:

Show:

```
Registry Backup

A backup will be created before modification.


Location:

xxxxx


[Create Backup]

[Continue]
```

---

# Registry Restore UI

Provide:

Backup List

↓

Date

↓

Size

↓

Restore Button


---

# Uninstaller Module UI Design

## Purpose

Provide complete application removal.

Including:

Installed Programs

↓

System Apps

↓

Residual Files

↓

Registry Residues

↓

Startup Entries

↓

Extensions

---

# Installed Application List

Design:

Virtualized table.


Columns:

Application Icon

↓

Name

↓

Publisher

↓

Install Date

↓

Size

↓

Risk


---

# Search Function

Must support:

Instant Search

↓

Publisher Search

↓

Category Search


---

# Application Detail Panel

When selecting application:

Show:

Application Information

↓

Installation Location

↓

Related Files

↓

Registry Entries

↓

Services

↓

Startup Items


---

# Uninstall Action

Button:

"Complete Remove"


Not:

"Delete"


Because:

Removal includes residual cleanup.

---

# Residual Scan UI

This is a key differentiating feature.

Must clearly show:

Traditional uninstall

↓

LeslieCleaner enhanced cleanup


---

# Residual Detection Progress

Display:

```
Analyzing remaining data...


Checking:

Files

Registry

Services

Startup

Configuration


Confidence:

95%
```

---

# Residual Result Classification

Categories:

Required Residual

↓

Recommended Cleanup

↓

Optional Cleanup


---

# Residual Speed Optimization Display

Show:

Fast Analysis Engine

↓

Indexed Search

↓

Application Fingerprint


Example:

```
Found 328MB residual data

Analysis completed in 8 seconds
```

---

# Startup Manager UI Design

## Purpose

Optimize boot speed safely.

Critical requirement:

Separate Microsoft system startup items and user-installed software startup items.

---

# Startup Main Page

Layout:

```
Startup Optimization


Boot Impact:

High


Estimated Boot Time:

25 seconds



Categories:

[Microsoft System]

[User Applications]

```

---

# Startup Classification Design

Must strictly separate:

## Microsoft System Startup

Contains:

  Components

↓

Microsoft Services

↓

System Drivers

↓

Essential Components


---

## User Installed Startup

Contains:

Third-party Applications

↓

Auto Updaters

↓

Launchers

↓

Background Software


---

# Microsoft Startup Section UI

Display:

```
Microsoft System Startup

⚠ Protected Area


  Security

  Defender

System Services


Modification:

Restricted
```

---

# Protection Rules

Microsoft system items:

Default:

Protected


Actions:

View

↓

Explain

↓

Advanced User Unlock


---

# User Startup Section UI

Display:

```
User Applications


Chrome Update

Discord

Adobe

Cloud Sync


Impact:

High
```

---

# Startup Item Information Panel

Show:

Name

↓

Publisher

↓

Location

↓

Impact

↓

Last Modified

↓

Recommendation


---

# Startup Actions

Allow:

Disable

↓

Enable

↓

Delay Start

↓

Remove Entry


---

# Startup Optimization Recommendation

Example:

```
You can reduce startup time by:

15 seconds


Recommended actions:

Disable 5 unnecessary applications
```

---

# Startup Batch Operation

Support:

Select All User Applications

↓

Disable Selected

↓

Enable Selected


Never batch modify Microsoft protected items.

---

# Startup Safety Rules

Before modification:

Create backup

↓

Record original state

↓

Allow restore

---

# File Analyzer UI Design

Purpose:

Help users understand disk usage.


---

# File Analysis Dashboard

Display:

Disk Usage

↓

Large Files

↓

Duplicate Files

↓

Old Files


---

# Visualization

Use:

Clean charts

↓

Treemap

↓

Category cards


Avoid:

Complex enterprise charts.


---
---

# File Shredder UI Design

## Purpose

Provide secure permanent file deletion.

The UI must clearly distinguish:

Normal Delete

↓

Recycle Bin Delete

↓

Secure Shredding


Users must understand that shredded files cannot normally be recovered.

---

# Shredder Main Page Layout

```
------------------------------------------------

Secure File Shredder

------------------------------------------------


Drop files here


        +

[ Select Files ]

[ Select Folder ]


------------------------------------------------


Selected Items:

125 Files

Size:

8.5GB


------------------------------------------------


Shredding Method:

Quick

Standard

Advanced


------------------------------------------------


[ Start Shredding ]

------------------------------------------------
```

---

# Drag And Drop Design

Support:

Drag Files

↓

Drag Folders

↓

Multiple Selection


Display:

File Count

↓

Total Size

↓

Estimated Time


---

# Shredding Algorithm Selection

Provide understandable descriptions.

---

## Quick Mode

Description:

Fast deletion.

Suitable for:

Low-risk files.


---

## Standard Mode

Description:

Multiple overwrite.

Suitable for:

Personal files.


---

## Advanced Mode

Description:

Maximum security deletion.

Suitable for:

Sensitive data.


---

# Shred Confirmation Dialog

Before execution:

Show:

```
Warning

These files will be permanently deleted.

Recovery may be impossible.


Files:

125


Size:

8.5GB


[Cancel]

[Confirm Shred]
```

---

# Shred Progress UI

Display:

Current File

↓

Progress

↓

Completed Count

↓

Remaining Count

↓

Estimated Time


---

# Privacy Cleaner UI Design

## Purpose

Protect user privacy.

Categories:

Browser History

↓

Recent Files

↓

Search History

↓

Temporary Records

↓

Application Traces


---

# Privacy Dashboard

Display:

Privacy Risk Score


Example:

```
Privacy Status

Medium Risk


12 privacy traces found
```

---

# Privacy Scan Categories

Cards:

```
Browser Privacy

125MB


  History

20MB


Recent Documents

5MB


Application Traces

300MB

```

---

# Browser Cleaner UI Design

Supported:

Chrome

↓

Edge

↓

Firefox

↓

Brave

↓

Opera


---

# Browser Profile Detection

Display:

Browser Name

↓

Profile

↓

Account

↓

Data Size


---

# Browser Cleanup Options

Allow selection:

Cache

↓

Cookies

↓

History

↓

Downloads History

↓

Session Data


---

# Cookie Protection

Important:

Cookies may contain login sessions.

Default:

Do not delete important cookies.


Provide:

Protected Cookies

↓

Custom Selection

↓

Whitelist


---

# Settings Center UI Design

## Purpose

Central configuration management.


---

# Settings Layout

Sidebar:

General

↓

Appearance

↓

Cleaning

↓

Privacy

↓

Performance

↓

Security

↓

Updates

↓

Advanced


---

# General Settings

Options:

Start With  

↓

Minimize To Tray

↓

Notification

↓

Default Scan Mode


---

# Appearance Settings

Options:

Theme:

Light

↓

Dark

↓

System


Accent Color:

Blue

↓

Custom


Animation:

Enable / Disable


---

# Performance Settings

Options:

CPU Usage Limit

↓

Scan Threads

↓

Background Optimization

↓

Battery Mode


---

# Security Settings

Options:

Require Confirmation

↓

Automatic Backup

↓

Restore Protection

↓

Admin Permission


---

# Update Center UI Design

## Purpose

Manage software updates.


---

# Update Page

Display:

Current Version

↓

Latest Version

↓

Release Notes

↓

Update Button


---

# Update Progress

Show:

Download

↓

Verification

↓

Installation

↓

Restart Requirement


---

# Update Safety

Before update:

Create Backup

↓

Verify Package

↓

Rollback Available


---

# AI Assistant UI Design

## Purpose

Provide intelligent optimization recommendations.

AI is an assistant.

Not a replacement for user control.


---

# AI Panel Design

Position:

Optional side panel.


---

# AI Capabilities

Examples:

"Why is my computer slow?"

↓

"Which startup items can be disabled?"

↓

"Can I delete this file?"

↓

"How can I free more space?"


---

# AI Recommendation Card

Structure:

Problem

↓

Analysis

↓

Recommendation

↓

Action Button


Example:

```
Startup Impact High


Discord launches at startup.


Recommendation:

Disable automatic startup.


[Optimize]
```

---

# AI Safety Rules

AI cannot:

Automatically delete important files.

↓

Modify registry without confirmation.

↓

Disable system components.

↓

Execute dangerous operations.


---

# Plugin Manager UI Design

## Purpose

Manage extension capabilities.


---

# Plugin List

Display:

Plugin Icon

↓

Name

↓

Version

↓

Publisher

↓

Status


---

# Plugin Actions

Enable

↓

Disable

↓

Remove

↓

Update


---

# Plugin Security Display

Show:

Permission Required

↓

Data Access

↓

Risk Level


---

# Advanced User Panel

For experienced users.

Contains:

Detailed Logs

↓

Advanced Scan Options

↓

Experimental Features


---

# Do Not Expose Advanced Features By Default

Advanced functions should be:

Collapsed

↓

Hidden

↓

Optional


---

# White Premium Theme Specification

## Overall Feeling

Target:

Professional productivity software.

Not:

System cleaner advertisement software.


---

# Background

Use:

Warm white

↓

Soft gray layers


---

# Cards

Characteristics:

Rounded corners

↓

Soft shadow

↓

Large spacing


---

# Buttons

Primary:

Solid accent color


Secondary:

Light surface


Danger:

Only for destructive operations


---

# Tables

Use:

Minimal borders

↓

Alternating spacing

↓

Hover highlight


---

# Dialogs

Design:

Centered

↓

Rounded

↓

Clear hierarchy


---

# AI UI Generation Rules

When AI generates UI:

Must follow:

Premium white interface

↓

Modern Fluent style

↓

Clean layout

↓

High spacing

↓

Soft shadows

↓

Professional typography


---

# AI Must Avoid

Dark heavy theme

↓

Old   style

↓

Too many gradients

↓

Too many colors

↓

Crowded panels

↓

Cheap visual effects


---

# UI Acceptance Criteria

UI is accepted only when:

✓ Consistent design system

✓ Smooth interaction

✓ Clear information hierarchy

✓ Premium visual quality

✓ Responsive layout

✓ Accessible

✓ High performance

✓ No unnecessary complexity


---

# Final UI Statement

LeslieCleaner UI should make users feel:

"This is a professional system optimization product."

Not:

"This is another simple cleaning tool."

The interface represents:

Trust

↓

Quality

↓

Technology

↓

Performance

END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

