# LeslieCleaner Android Development Bible - 13_UI_UX_Design_Specification

> Android Prompt ID: AP-MAIN-13-UI-UX-DESIGN-SPECIFICATION-MD  
> Source Prompt: `Main\13_UI_UX_Design_Specification.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: Jetpack Compose, Material 3, accessibility, adaptive layouts, touch/keyboard semantics and state restoration  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **Jetpack Compose, Material 3, accessibility, adaptive layouts, touch/keyboard semantics and state restoration**. The result must be useful on stock Android,
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

- [ ] Add/confirm requirement IDs `AP-MAIN-13-UI-UX-DESIGN-SPECIFICATION-MD` in the Android traceability matrix.
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

Version:

1.0


Document Type:

UI/UX Design Specification


Priority:

★★★★★


---

# Purpose


This document defines the complete UI/UX design standard of LeslieCleaner.


The objective:


Create a professional   desktop application interface.


The design must feel:


Premium

↓

Clean

↓

Modern

↓

Professional

↓

Trustworthy


---

# Design Positioning


LeslieCleaner is not a technical tool.

It is a consumer-level professional optimization product.



The UI should communicate:


"Safe"

↓

"Reliable"

↓

"Powerful"

↓

"Easy to use"



---

# Visual Reference


Design inspiration:


##   11 Fluent Design


Characteristics:


Clean spacing

↓

Rounded corners

↓

Soft shadows

↓

Light surfaces


---

## Twitter / X Interface


Reference:


Modern white background


↓

Clear content hierarchy


↓

Minimal visual noise


↓

Professional typography



---

## Notion Style


Reference:


Information organization


↓

Cards


↓

Simple interactions



---

# Overall Theme Requirement


## Main Theme


Premium White Theme



---

# Color Philosophy


Do not use:


Pure cheap white.



Use:


Soft professional white.



---

# Color System


## Background


```
#F8F9FB

```


Purpose:


Main application background.



---

## Card Background


```
#FFFFFF

```


Purpose:


Content containers.



---

## Primary Text


```
#1F2937

```


Purpose:


Main titles.



---

## Secondary Text


```
#6B7280

```


Purpose:


Descriptions.



---

## Border


```
#E5E7EB

```


Purpose:


Subtle separation.



---

## Accent Color


Use:


Professional blue.


Example:


```
#2563EB

```


Purpose:


Primary actions.



---

# Color Usage Rules


Do not:


Use excessive bright colors.



Each page:


Maximum:


3-4 main colors.



---

# Typography Standard


Recommended:


Microsoft YaHei


or


Segoe UI



---

# Font Hierarchy


## Main Title


Size:


28-32px



Weight:


Bold



---

## Section Title


Size:


20-24px



Weight:


Semi Bold



---

## Normal Text


Size:


14-16px



---

## Description


Size:


12-14px



---

# Layout Philosophy


Use:


Large spacing


↓

Clear grouping


↓

Card-based structure



---

# Window Layout


Main structure:


```

┌───────────────────────────┐

│ Header                    │

├──────────┬────────────────┤

│ Sidebar  │ Content Area   │

│          │                │

│          │                │

└──────────┴────────────────┘


```



---

# Header Design


Contains:


Logo


↓

Application Name


↓

Current Status


↓

Settings


↓

User Actions



---

# Header Requirement


Height:


60-72px



---

# Sidebar Design


Purpose:


Main navigation.



---

# Sidebar Items


Required:


```

Dashboard

Cleaner

Registry

Uninstaller

Startup

Privacy

Browser

Shredder

Analyzer

AI Assistant

Settings


```



---

# Sidebar Style


Requirements:


Rounded selection background


↓

Smooth animation


↓

Icon + Text



---

# Sidebar Width


Recommended:


240px



---

# Content Area


Must support:


Scrolling


↓

Dynamic cards


↓

Responsive layout



---

# Card Design Standard


All information blocks should use cards.



---

# Card Properties


Rounded corner:


12-16px



---

Shadow:


Very soft.



---

Padding:


20-24px



---

# Button Design


## Primary Button


Example:


"Start Scan"



Style:


Rounded


↓

Clear color


↓

Hover animation



---

# Secondary Button


Example:


"Cancel"



Style:


Outline


↓

Light background



---

# Button Height


Recommended:


40-48px



---

# Animation Standard


Animations must be:


Smooth


↓

Fast


↓

Not distracting



---

# Animation Duration


Recommended:


150-300ms



---

# Loading Animation


Use:


Progress indicator


↓

Skeleton loading


↓

Status animation



---

# Dashboard Design


## Purpose


First screen after launch.



---

# Dashboard Layout


```

System Health


↓

Quick Actions


↓

Storage Overview


↓

Optimization Suggestions


↓

Recent Activity


```



---

# System Health Card


Display:


Overall Score



Example:


```
92

Excellent


```



---

# Health Score Factors


Calculate:


Junk Files


↓

Startup Impact


↓

Privacy Risk


↓

System Issues



---

# Quick Action Area


Buttons:


```

Quick Clean

Deep Scan

Manage Startup

Uninstall Software


```



---

# Storage Card


Display:


```

Total Storage

Used Space

Free Space

Cleanable Space


```



---

# Optimization Suggestion Card


Show:


AI recommendations.



Example:


```

Chrome cache occupies 8GB.

Recommended cleanup.


```



---

# Recent Activity


Display:


Recent:


Cleanups


↓

Scans


↓

Uninstall operations



---

# Dashboard Requirements


Must provide:


At first glance:


Current computer status.



User should understand:


"Is my PC healthy?"

within:


5 seconds.



---
---

# Cleaner Module UI Design


## Purpose


Provide simple and powerful system cleanup experience.



---

# Cleaner Page Layout


Structure:


```

Page Header


↓

Scan Control Area


↓

Category Cards


↓

Scan Result Area


↓

Cleanup Action Area


```



---

# Page Header


Display:


Title:


```
System Cleaner

```


Description:


```
Analyze and remove unnecessary files safely.

```



---

# Scan Control Area


Main element:


Large Scan Button



Example:


```
Start Smart Scan

```



---

# Scan Modes


Provide:


```

Quick Scan


Smart Scan


Deep Scan


Custom Scan


```



---

# Quick Scan


Target:


Common junk files.



Includes:


Temporary Files

↓

Cache

↓

Logs



---

# Smart Scan


Default mode.



Analyze:


System


↓

Applications


↓

Browser


↓

Privacy



---

# Deep Scan


Advanced users.



Includes:


Large file analysis


↓

Residual detection


↓

Deep cache detection



---

# Custom Scan


Allow user selection:


```

□   Cache

□ Browser Cache

□ Application Cache

□ Log Files

□ Temporary Files


```



---

# Scan Progress UI


During scanning:


Display:


Current Module


↓

Current Path


↓

Progress Percentage


↓

Estimated Time



---

# Example:


```

Scanning Chrome Cache...


45%


3.2GB Found


```



---

# Scan Result UI


After scan:


Display cards:



## Junk Category Card


Example:


```

Temporary Files

5.6GB


```



---

## Risk Level


Show:


```

Safe

↓

Attention

↓

Warning


```



---

# Result List


Each item:


Contains:


Checkbox


↓

Name


↓

Location


↓

Size


↓

Risk



---

# Batch Operation Requirement


Every list must include:


```

□ Select All


□ Select None


□ Invert Selection


```



---

# Cleanup Button


Primary action:


```
Clean Selected Items

```



---

# Before Cleanup


Show confirmation:


Display:


```

Items selected:

156


Space to free:

12.5GB


Backup:

Enabled


```



---

# Registry Cleaner UI Design


## Purpose


Safely optimize   Registry.



---

# Registry Page Layout


```

Registry Health Score


↓

Scan Categories


↓

Problem List


↓

Backup Area


↓

Repair Action


```



---

# Registry Health Card


Display:


Example:


```

Registry Health

Excellent


98%


```



---

# Scan Categories


Cards:


```

Invalid Paths


Missing Applications


Unused Extensions


Broken Associations


Obsolete Entries


```



---

# Registry Risk Explanation


Every detected item shows:


```

Why detected


↓

Risk level


↓

Recommended action


```



---

# Registry Warning Area


Always display:


```

Registry changes will be backed up automatically.


```



---

# Backup Panel


Display:


Latest Backup


↓

Restore Button



---

# Repair Button


Primary:


```
Fix Selected Problems

```



---

# Software Uninstaller UI Design


## Purpose


Provide complete application removal.



---

# Layout


```

Search Bar


↓

Application List


↓

Application Detail


↓

Uninstall Action


↓

Residual Cleanup


```



---

# Application List


Each item:


Display:


Icon


↓

Name


↓

Publisher


↓

Version


↓

Size


↓

Risk



---

# Search Function


Support:


Application Name


↓

Publisher


↓

Install Location



---

# Batch Uninstall Requirement


Must support:


```

Select All


Select None


Batch Uninstall


```



---

# Application Detail Panel


Display:


```

Install Path


Installation Date


Related Files


Startup Items


Services


Registry Entries


```



---

# Uninstall Workflow UI


Steps:


```

1. Analyze


↓

2. Backup


↓

3. Official Uninstall


↓

4. Residual Scan


↓

5. Cleanup


```



---

# Residual Scan Optimization Display


Important:



Do not show:


"Scanning entire disk"



Show:


```

Analyzing application fingerprint...


Searching related files...


Checking registry...


```



---

# Residual Result UI


Display:


```

Remaining Files:

320


Registry Entries:

45


Services:

2


```



---

# Startup Manager UI Design


## Purpose


Optimize   boot speed safely.



---

# Critical Requirement


Startup items must be separated into two categories.



---

# Startup Page Layout


```

Boot Performance Score


↓

Microsoft System Startup


↓

User Installed Startup


↓

Optimization Suggestions


```



---

# Microsoft System Startup Section


Title:


```
Microsoft System Components

```



---

# Display:


```

  Defender

  Update

System Services


```



---

# Protection Badge


Each system item:


Show:


```
Protected

```



---

# Operation Restrictions


Default:


Cannot directly disable.



If user attempts:


Show warning.



---

# User Installed Startup Section


Title:


```
Third-party Application Startup

```



---

# Display:


```

Application Name


Publisher


Impact


Recommendation


Status


```



---

# Impact Level


Show:


```

Low Impact


Medium Impact


High Impact


```



---

# Startup Actions


Buttons:


```

Disable


Enable


Delay Start


Restore


```



---

# Startup Batch Operation


Support:


```

□ Select All


□ Disable Selected


□ Enable Selected


```



---

# File Shredder UI Design


## Purpose


Secure permanent deletion.



---

# Layout


```

File Drop Area


↓

Security Level


↓

File List


↓

Destroy Button


```



---

# File Drop Area


Support:


Drag and Drop



---

# Shred Algorithm Selection


Options:


```

Quick


Standard


Advanced


```



---

# Security Warning


Before execution:


Display:


```

This operation cannot be undone.

```



---

# Destroy Button


Requires:


Confirmation



---

# Browser Cleaner UI Design


## Supported Browsers


```

Chrome

Edge

Firefox

Brave


```



---

# Layout


```

Browser Selection


↓

Profile Selection


↓

Cleaning Options


↓

Result


```



---

# Cleaning Options


```

□ Cache


□ History


□ Cookies


□ Download Records


□ Session Data


```



---

# Cookie Safety


Default:


Protected.



Require:


User confirmation.



---

# Settings UI Design


## Categories


```

General


Appearance


Performance


Security


Privacy


Advanced


```



---

# Appearance Settings


Include:


Theme


↓

Animation


↓

Transparency


↓

Font Size



---

# Performance Settings


Include:


CPU Limit


↓

Memory Limit


↓

Scan Threads


↓

Background Tasks



---

# Security Settings


Include:


Automatic Backup


↓

Confirmation Level


↓

Protected Paths



---

# AI Assistant UI Design


## Purpose


Provide intelligent recommendations.



---

# AI Panel Layout


```

Question Input


↓

System Analysis


↓

Recommendation Cards


↓

User Action


```



---

# Recommendation Card


Display:


Problem


↓

Reason


↓

Impact


↓

Suggested Action



---

# AI Action Rules


AI can:


Explain


↓

Recommend


↓

Guide



AI cannot:


Delete


↓

Modify registry


↓

Change startup


without confirmation.



---

# UI Final Quality Requirement


LeslieCleaner interface must feel like:


A modern professional   optimization product.


Not:


A technical utility tool.



---

# UI Must Achieve:


✓ Premium white visual style

✓ Clear information hierarchy

✓ Safe operation feeling

✓ Fast interaction

✓ Beginner friendly

✓ Professional enough for advanced users


END OF DOCUMENT
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

