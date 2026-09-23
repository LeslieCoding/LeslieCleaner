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