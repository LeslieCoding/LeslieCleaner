# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Data Model And Database Design Specification


Priority:

★★★★★


---

# Purpose


This document defines the complete database architecture of LeslieCleaner.


The goal:


Provide a stable, efficient, safe data storage system.


The database must support:


User Configuration

↓

Scan Records

↓

Cleanup History

↓

Application Information

↓

Startup Management

↓

Registry Backup

↓

System Analysis

↓

AI Recommendations



---

# Database Technology


Recommended:


SQLite


Version:


SQLite 3.x



---

# Database Design Philosophy


The database must be:


Lightweight


↓

Fast


↓

Reliable


↓

Easy Backup


↓

Easy Migration



---

# Database Location


Recommended:


Normal Installation:


```

C:\ProgramData\LeslieCleaner\Database


```



Portable Mode:


```

.\Data\Database


```



---

# Main Database File


Name:


```
LeslieCleaner.db


```



---

# Database Characteristics


SQLite advantages:


No server dependency


↓

Fast local access


↓

Easy backup


↓

Suitable for desktop applications



---

# Database Access Architecture


Database access must follow:


```

ViewModel


↓

Service


↓

Repository


↓

SQLite


```



---

# Forbidden


Direct database operation:


UI


↓

SQLite



---

# ORM Recommendation


Recommended:


Entity Framework Core SQLite


or


Dapper



---

# Preferred


Dapper


Reason:


Lightweight


↓

High performance


↓

Suitable for desktop software



---

# Database Naming Rules


Table:


PascalCase


Example:


```
StartupItems


```



---

# Column:


PascalCase


Example:


```
CreatedTime


```



---

# Primary Key


Standard:


```
Id


```



---

# Time Fields


Use:


```
CreatedTime


UpdatedTime


```



Format:


UTC Time



---

# Database Table Overview


Main tables:


```

UserSettings


↓

ScanRecords


↓

ScanItems


↓

CleanupRecords


↓

Applications


↓

StartupItems


↓

RegistryBackups


↓

OperationLogs


↓

AIRecommendations


```



---

# Table 1

# UserSettings


Purpose:


Store user preferences.



---

# Structure


```

UserSettings


Id


Key


Value


ValueType


Description


CreatedTime


UpdatedTime


```



---

# Field Explanation


## Id


Primary key.



---

## Key


Configuration name.



Example:


```
Theme


```



---

## Value


Configuration value.



Example:


```
Light


```



---

## ValueType


Data type.



Example:


```
String


Boolean


Integer


```



---

## Description


Configuration explanation.



---

# Example Data


```

Theme

Light


ScanThreads

8


AutoBackup

true


```



---

# Table 2

# ScanRecords


Purpose:


Record every scan operation.



---

# Structure


```

ScanRecords


Id


ScanType


StartTime


EndTime


Duration


TotalItems


TotalSize


Status


CreatedTime


```



---

# Field Explanation


## ScanType


Example:


```
QuickScan

DeepScan

CustomScan


```



---

## Status


Example:


```
Completed


Cancelled


Failed


```



---

## TotalSize


Unit:


Bytes



---

# Usage


Display:


Scan history


↓

Performance analysis


↓

Statistics



---

# Table 3

# ScanItems


Purpose:


Store detailed scan results.



---

# Structure


```

ScanItems


Id


ScanRecordId


ItemType


Name


Path


Size


RiskLevel


Selected


Status


CreatedTime


```



---

# Field Explanation


## ScanRecordId


Relationship:


```

ScanRecords.Id


```



---

## ItemType


Examples:


```
TempFile


Cache


Registry


Residual


```



---

## RiskLevel


Values:


```

Low


Medium


High


Critical


```



---

## Selected


User selection state.



Example:


```

true


false


```



---

## Status


Example:


```

Detected


Cleaned


Skipped


Failed


```



---

# Table 4

# CleanupRecords


Purpose:


Store cleanup execution history.



---

# Structure


```

CleanupRecords


Id


ScanRecordId


CleanType


ItemCount


CleanSize


StartTime


EndTime


Status


CreatedTime


```



---

# Field Explanation


## CleanType


Examples:


```

JunkClean


RegistryRepair


ResidualClean


```



---

## CleanSize


Amount of released storage.



Unit:


Bytes



---

# Cleanup History Usage


Display:


Dashboard statistics


↓

Recent activity


↓

User reports



---

# Table 5

# Applications


Purpose:


Store installed software information.



---

# Structure


```

Applications


Id


Name


Publisher


Version


InstallPath


InstallDate


Size


UninstallCommand


Fingerprint


RiskLevel


CreatedTime


UpdatedTime


```



---

# Field Explanation


## Name


Application name.



---

## Publisher


Software developer.



---

## InstallPath


Installation directory.



---

## UninstallCommand


Official uninstall command.



---

## Fingerprint


Used for residual detection.



Contains:


Files


↓

Registry paths


↓

Services


↓

Startup entries



---

# Table Usage


Used by:


Software Manager


↓

Uninstaller


↓

Residual Scanner



---
---

# Table 6

# StartupItems


## Purpose


Store System startup item information.


This table is critical for startup optimization.



---

# Design Requirement


Startup items must be strictly classified:


```

Microsoft System Startup


↓

User Installed Application Startup


```



---

# Structure


```

StartupItems


Id


Name


Publisher


Path


Command


StartupType


Category


ImpactLevel


Status


IsProtected


CanDisable


Enabled


CreatedTime


UpdatedTime


```



---

# Field Explanation



## Name


Startup item name.



Example:


```

System Defender


Chrome


OneDrive


```



---

## Publisher


Software publisher.



Example:


```

Microsoft Corporation


Google LLC


```



---

## Path


Executable path.



Example:


```

C:\Program Files\xxx\xxx.exe


```



---

## Command


Startup command.



---

## StartupType


Source type.



Values:


```

Registry


StartupFolder


TaskScheduler


Service


```



---

## Category


Critical field.



Values:



### MicrosoftSystem


Represents:


System built-in startup components.



Example:


```

System Security


System Update


System Services


```



---

### UserApplication


Represents:


User-installed software startup items.



Example:


```

Steam


Discord


Adobe


```



---

## ImpactLevel


Startup impact.



Values:


```

Low


Medium


High


Critical


```



---

## Status


Current state.



Values:


```

Enabled


Disabled


Delayed


```



---

## IsProtected


Whether protected.



Example:


```

true


```



Meaning:


Cannot be disabled normally.



---

## CanDisable


Whether user can disable.



Example:


```

false


```



---

## Enabled


Current startup status.



---

# Startup Safety Rules


Before disabling:



Check:


```

Category


↓

IsProtected


↓

CanDisable


↓

RiskLevel


```



---

# Important Logic


If:


```

Category = MicrosoftSystem


AND


IsProtected = true


```



Then:


Disable operation blocked.



---

# Table 7

# RegistryBackups


## Purpose


Store registry backup information.



---

# Structure


```

RegistryBackups


Id


BackupName


RegistryPath


BackupFile


BackupSize


OperationType


CreatedTime


Status


```



---

# Field Explanation



## BackupName


Example:


```

BeforeRegistryRepair_001


```



---

## RegistryPath


Modified registry location.



Example:


```

HKCU\Software\xxx


```



---

## BackupFile


Backup file path.



Example:


```

Backup\Registry\001.reg


```



---

## OperationType


Values:


```

Repair


Delete


Modify


Restore


```



---

## Status


Values:


```

Created


Used


Restored


Failed


```



---

# Registry Backup Rules


Before:


Any registry modification



Must:


Create backup record.



---

# Table 8

# OperationLogs


## Purpose


Record all important user operations.



---

# Structure


```

OperationLogs


Id


Module


Operation


Description


Level


Result


ErrorMessage


CreatedTime


```



---

# Field Explanation



## Module


Example:


```

Cleaner


Registry


Startup


Uninstaller


```



---

## Operation


Example:


```

Scan


Delete


Restore


DisableStartup


```



---

## Level


Values:


```

Info


Warning


Error


Critical


```



---

## Result


Values:


```

Success


Failed


Cancelled


```



---

# Logging Requirement


Every dangerous action requires logging.



Examples:


Registry modification


↓

File deletion


↓

Startup change


↓

Uninstall operation



---

# Table 9

# AIRecommendations


## Purpose


Store AI analysis results.



---

# Structure


```

AIRecommendations


Id


Category


Problem


Analysis


Suggestion


Impact


Status


CreatedTime


```



---

# Field Explanation



## Category


Examples:


```

Startup


Storage


Privacy


Performance


```



---

## Problem


Detected issue.



Example:


```

Chrome cache is large


```



---

## Analysis


AI explanation.



---

## Suggestion


Recommended action.



---

## Impact


Example:


```

Improve startup speed


Free storage space


```



---

## Status


Values:


```

Pending


Accepted


Rejected


Completed


```



---

# Database Index Design


Indexes improve query performance.



---

# Required Indexes



## Applications


Index:


```
Name


Publisher


```



Purpose:


Fast search.



---

## StartupItems


Index:


```
Category


Enabled


```



Purpose:


Startup filtering.



---

## ScanItems


Index:


```
ScanRecordId


ItemType


```



Purpose:


Fast result loading.



---

## OperationLogs


Index:


```
Module


CreatedTime


```



---

# Database Optimization Rules


## Use Pagination


Large lists must not load all data.



Example:


Applications:


1000+ records



---

# Query Limit


Use:


```
LIMIT


OFFSET


```



---

# Transaction Usage


Important operations require transactions.



Examples:


Cleanup


↓

Registry Repair


↓

Database Migration



---

# Transaction Example Flow


```

Begin Transaction


↓

Backup


↓

Modify


↓

Verify


↓

Commit


```



Failure:


Rollback.



---

# Database Backup Strategy


Backup:


```

LeslieCleaner.db


```



---

# Backup Timing


Recommended:


Before:


Major upgrade


↓

Database migration


↓

Large cleanup operation



---

# Database Integrity Check


Periodically verify:



```

Table Integrity


↓

Index Integrity


↓

Data Consistency


```



---

# Database Recovery


If corrupted:


Process:


```

Close Application


↓

Restore Backup


↓

Verify


↓

Restart


```



---

# Data Security Rules


Sensitive data:



Must not store:


Passwords


↓

Browser cookies


↓

Private content



---

# Privacy Requirement


Database logs should store:


Operation information only.



---

# Database Performance Target


Normal query:


<50ms



Large query:


<500ms



---

# Database Final Requirement


LeslieCleaner database must provide:


Reliable storage


↓

Fast querying


↓

Safe recovery


↓

Easy migration


↓

Long-term stability



END OF DOCUMENT