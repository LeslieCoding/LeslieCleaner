# LeslieCleaner Development Bible

Version

1.0

Document Type

Database Design Specification

Priority

★★★★★

This document defines the complete database architecture of LeslieCleaner.

The database is responsible for storing:

Application Information

↓

Cleaning Rules

↓

Application Fingerprints

↓

Scan Results

↓

Cleanup History

↓

Backup Records

↓

Startup Information

↓

User Preferences

↓

Performance Data


The database must be:

Fast

↓

Reliable

↓

Lightweight

↓

Easy to migrate

↓

Safe from corruption

---

# Database Technology Standard

## Recommended Database

SQLite


Reason:

Local desktop application.

No server dependency.

Fast read/write.

Low resource usage.

Easy backup.

---

# Database Location


Recommended:

```
%ProgramData%\LeslieCleaner\Database\
```


Database file:


```
LeslieCleaner.db
```


---

# Database Architecture


```

Application Layer


        ↓


Repository Layer


        ↓


Database Service


        ↓


SQLite Database


```


---

# Database Access Rules


All database operations must go through:


```
IDatabaseService


        ↓


Repository


        ↓


SQLite


```


---

# Forbidden


Direct SQL inside:

ViewModel

↓

UI

↓

Business Logic


---

# Database Connection Rules


Required:


Connection Pooling

↓

Prepared Statements

↓

Async Queries

↓

Transaction Support


---

# Database Security


Database must support:


Integrity Check

↓

Backup

↓

Recovery

↓

Migration


---

# Database Naming Rules


Table names:

PascalCase


Example:


```
Applications

ScanResults

CleanupHistory

```


---

Column names:

PascalCase


Example:


```
ApplicationId

InstallPath

CreatedTime


```


---

# Core Database Tables Overview


Main tables:


```

Applications

ApplicationFingerprints

InstalledComponents

CleanRules

ScanResults

ScanItems

CleanupHistory

BackupRecords

StartupItems

PrivacyRecords

BrowserProfiles

Settings

PerformanceLogs


```


---

# Table 1

# Applications


## Purpose


Store installed software information.


Used by:


Uninstaller

↓

Residual Scanner

↓

Startup Manager


---

Structure:


```
Applications

{

ApplicationId

Name

Publisher

Version

InstallPath

InstallDate

InstallSize

UninstallCommand

PublisherUrl

ExecutablePath

IconPath

FingerprintId

RiskLevel

Status

CreatedTime

UpdatedTime

}

```


---

# Field Description


## ApplicationId


Type:

INTEGER


Primary Key.


---

## Name


Type:

TEXT


Application display name.


Example:


```
Google Chrome
```


---

## Publisher


Type:

TEXT


Software publisher.


Example:


```
Google LLC
```


---

## Version


Type:

TEXT


Current version.


---

## InstallPath


Type:

TEXT


Installation directory.


---

## InstallSize


Type:

INTEGER


Unit:

Bytes


---

## UninstallCommand


Type:

TEXT


Official uninstall command.


---

## FingerprintId


Type:

INTEGER


Reference:

ApplicationFingerprints


---

## RiskLevel


Type:

INTEGER


Range:


```
0-100
```


---

## Status


Values:


```
Installed

Removed

Unknown

```


---

# Index Design


Required indexes:


```
INDEX Applications_Name


INDEX Applications_Publisher


INDEX Applications_FingerprintId


INDEX Applications_Status


```


---

# Table 2

# ApplicationFingerprints


## Purpose


Store software identification fingerprints.


Core feature for fast residual detection.


---

Structure:


```
ApplicationFingerprints

{

FingerprintId

ApplicationId

FilePatterns

RegistryPatterns

ServicePatterns

StartupPatterns

TaskPatterns

BrowserPatterns

CreatedTime

UpdatedTime

}

```


---

# FilePatterns


Store:


```

Executable Files

Configuration Files

Cache Paths

Log Paths


```


Example:


```
Chrome.exe

Google\Chrome\User Data

```


---

# RegistryPatterns


Store:


```

Registry Keys

Registry Values

Associations


```


---

# ServicePatterns


Store:


```

  Services

Background Processes


```


---

# StartupPatterns


Store:


```

Startup Registry

Startup Folder

Scheduled Tasks


```


---

# Table 3

# InstalledComponents


## Purpose


Store application-related components.


Used for:

Complete uninstall.


---

Structure:


```
InstalledComponents

{

ComponentId

ApplicationId

ComponentType

Path

Name

Size

Hash

CreatedTime

}

```


---

# ComponentType


Values:


```

File

Folder

Registry

Service

Startup

Task

Extension

```


---

# Table 4

# CleanRules


## Purpose


Store cleaning rules.


Supports:

System Cleaner

↓

Browser Cleaner

↓

Privacy Cleaner


---

Structure:


```
CleanRules

{

RuleId

Category

Name

Description

TargetPath

FilePattern

RiskLevel

Enabled

CreatedTime

UpdatedTime

}

```


---

# Rule Example


```
RuleId:

10001


Category:

BrowserCache


Name:

Chrome Cache


Target:

%LOCALAPPDATA%\Google\Chrome


Risk:

10


```


---

# Rule Design Principle


Rules must be:

Data-driven

↓

Editable

↓

Version controlled

↓

Expandable


---

# Rule Categories


```

System

Browser

Application

Privacy

Cache

Log

Temporary


```


---

# Table 5

# ScanResults


## Purpose


Store scan sessions.


---

Structure:


```
ScanResults

{

ScanId

ScanType

StartTime

EndTime

Duration

TotalItems

TotalSize

Status

CreatedTime

}

```


---

# ScanType


Values:


```

Quick

Smart

Deep

Registry

Privacy

Residual


```


---

# Scan Status


Values:


```

Running

Completed

Cancelled

Failed


```


---

# Table 6

# ScanItems


## Purpose


Store individual scan results.


---

Structure:


```
ScanItems

{

ItemId

ScanId

Path

Name

Type

Size

RiskLevel

Recommendation

Selected

Status

}

```


---

# ScanItems Optimization


Large table.


Required:


Indexes:


```
ScanId

Path

RiskLevel

Selected


```


---

# Database Performance Rules


Large tables must support:


Pagination

↓

Lazy Loading

↓

Index Query


---

# Database Transaction Rules


Operations requiring transactions:


Cleanup

↓

Uninstall

↓

Restore

↓

Backup


---

# Transaction Example


```

Begin Transaction


↓

Create Backup Record


↓

Modify Data


↓

Update History


↓

Commit


```


If failure:


Rollback.


---

# Database Backup Strategy


Automatic backup:


Before:

Registry modification

↓

Application uninstall

↓

Major cleanup


---

# Database Backup Content


Include:


```

Database File

Configuration

Rules

Fingerprint Data

History


```


---

# Database Maintenance


Scheduled:


Integrity Check

↓

Vacuum

↓

Index Optimization

↓

Old Data Cleanup


---
---

# Table 7

# CleanupHistory


## Purpose


Store all cleaning operations.

Used for:

Operation tracking

↓

User review

↓

Rollback

↓

Statistics


---

Structure:


```
CleanupHistory

{

HistoryId

OperationType

TargetType

TargetPath

TargetName

SizeBefore

SizeAfter

Result

ErrorMessage

Operator

StartTime

EndTime

CreatedTime

}

```


---

# OperationType


Values:


```

Clean

Delete

Uninstall

RegistryModify

StartupModify

Shred

Restore


```


---

# TargetType


Values:


```

File

Folder

Registry

Application

StartupItem

Service


```


---

# Result


Values:


```

Success

Failed

Cancelled

Rollback


```


---

# CleanupHistory Usage


Example:


User cleaned:

```
15GB Temporary Files
```


Record:


```
Operation:

Clean


Size:

15GB


Result:

Success


```


---

# History Query Optimization


Required indexes:


```

INDEX CleanupHistory_OperationType


INDEX CleanupHistory_CreatedTime


INDEX CleanupHistory_Result


```


---

# Table 8

# BackupRecords


## Purpose


Store backup information before dangerous operations.


Used by:


Registry Restore

↓

Uninstall Restore

↓

Startup Restore


---

Structure:


```
BackupRecords

{

BackupId

BackupType

RelatedId

BackupPath

Size

Status

CreatedTime

ExpiredTime

}

```


---

# BackupType


Values:


```

Registry

File

Application

Startup

Configuration


```


---

# Backup Status


Values:


```

Created

Valid

Restored

Expired

Deleted


```


---

# Backup Storage Strategy


Default:


```
%ProgramData%\LeslieCleaner\Backup\
```


---

# Backup Management Rules


Support:


Automatic Cleanup


↓

Manual Delete


↓

Restore


---

# Backup Retention


Default:


30 Days


Configurable.


---

# Backup Safety


Backup operation must:


Verify write permission


↓

Verify integrity


↓

Record metadata


---

# Table 9

# StartupItems


## Purpose


Store startup program information.


Used by:


Startup Manager


---

Structure:


```
StartupItems

{

StartupId

Name

Publisher

Path

Location

Type

Category

Enabled

ImpactLevel

RiskLevel

Recommendation

LastCheckedTime

}

```


---

# Type


Values:


```

RegistryRun

StartupFolder

Service

ScheduledTask

Driver


```


---

# Category


Critical requirement:


Must separate:


```

MicrosoftSystem

UserApplication


```


---

# MicrosoftSystem Category


Contains:


```

  Components

Microsoft Services

Security Components


```


---

# UserApplication Category


Contains:


```

Third Party Software

Auto Updater

Background Application


```


---

# Startup Protection Field


Add:


```
Protected

BOOLEAN

```


Purpose:


Prevent accidental disabling.


---

# Startup Impact Storage


Values:


```

Low

Medium

High


```


---

# Startup Recommendation


Examples:


```

Enable

Disable

Delay

Protected


```


---

# Table 10

# PrivacyRecords


## Purpose


Store privacy scanning information.


---

Structure:


```
PrivacyRecords

{

PrivacyId

Category

Application

Path

DataType

Size

RiskLevel

DetectedTime

}

```


---

# Category


Values:


```

Browser

RecentFiles

SearchHistory

ApplicationTrace

SystemTrace


```


---

# Privacy Safety


Sensitive data:


Must not store content.


Only store:


Metadata

↓

Location

↓

Risk


---

# Table 11

# BrowserProfiles


## Purpose


Store browser information.


---

Structure:


```
BrowserProfiles

{

ProfileId

BrowserName

Version

ProfilePath

UserProfile

LastScanTime

}

```


---

# Browser Data Tables


Optional:


```
BrowserCache

BrowserCookies

BrowserHistory


```


---

# BrowserCookies Rules


Never store:


Cookie Content


Only:


```
Domain

Size

Risk

Status


```


---

# Table 12

# Settings


## Purpose


Store user configuration.


---

Structure:


```
Settings

{

SettingId

Category

Key

Value

Type

UpdatedTime

}

```


---

# Setting Examples


```

Theme

Light


ScanMode

Smart


CpuLimit

50


AutoBackup

true


```


---

# Configuration Categories


```

General

Appearance

Performance

Security

Privacy

Advanced


```


---

# Table 13

# PerformanceLogs


## Purpose


Monitor application performance.


Used for:


Optimization

↓

Regression Detection


---

Structure:


```
PerformanceLogs

{

LogId

Module

Operation

Duration

CpuUsage

MemoryUsage

DiskIO

CreatedTime

}

```


---

# Performance Data Example


```

Module:

ResidualScanner


Duration:

8.5s


Memory:

220MB


CPU:

35%


```


---

# Database Migration System


## Purpose


Support future versions.


---

# Migration Table


```
DatabaseMigration

{

Version

Description

AppliedTime

}

```


---

# Migration Rules


Every database change requires:


Migration Script


↓

Version Number


↓

Rollback Strategy


---

# Example


Version:

1.1


Change:


Add AIRecommendation table


---

# Database Initialization Process


Application First Start:


```

Check Database


↓

Create Database


↓

Create Tables


↓

Create Indexes


↓

Insert Default Rules


↓

Verify Integrity


```


---

# Default Data Initialization


Insert:


Cleaning Rules


↓

System Protection Rules


↓

Microsoft Startup Database


↓

Application Categories


---

# SQL Coding Standard


Required:


Parameterized Queries


↓

Transaction


↓

Index Usage


---

# Forbidden SQL


Avoid:


```
SELECT *

```


unless required.


---

Avoid:


Large full-table scan.


---

# Repository Database Pattern


Example:


Interface:


```
IApplicationRepository

```


Implementation:


```
SqliteApplicationRepository

```


---

# Repository Responsibilities


Allowed:


Query

↓

Insert

↓

Update

↓

Delete


---

Forbidden:


Business Decision


↓

UI Logic


↓

User Interaction


---

# Database Error Handling


Every database operation must handle:


Connection Failure


↓

Locked Database


↓

Corrupted Data


↓

Migration Error


---

# Recovery Strategy


Database Error:


```

Detect


↓

Backup


↓

Repair


↓

Restore


↓

Notify User


```


---

# Database Performance Target


Startup Load:


<500ms


Simple Query:


<50ms


Large Query:


<500ms


---

# Database Final Requirement


LeslieCleaner database must support:


✓ Millions of scan records

✓ Fast search

✓ Safe rollback

✓ Application fingerprint analysis

✓ Residual detection

✓ Performance monitoring

✓ Future expansion


END OF DOCUMENT