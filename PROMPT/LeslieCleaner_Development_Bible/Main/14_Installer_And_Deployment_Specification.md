# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Installer And Deployment Specification


Priority:

★★★★★


---

# Purpose


This document defines the installation and deployment system of LeslieCleaner.


The goal:


Create a reliable personal System desktop software installation process.


LeslieCleaner is designed for:

Personal long-term usage


Therefore:


No commercial publishing system is required.


Focus on:


Easy installation

↓

Safe upgrade

↓

Clean uninstall

↓

Stable operation



---

# Deployment Philosophy


The installation system must be:


Simple

↓

Safe

↓

Customizable

↓

Easy to maintain



---

# Installer Technology Recommendation


Recommended:


## WiX Toolset


or


## Inno Setup


or


## NSIS



Preferred:


Inno Setup


Reason:


Simple

↓

Stable

↓

Supports custom paths

↓

Good System compatibility



---

# Installation Features Required


The installer must support:


✓ Custom installation location


✓ Administrator permission handling


✓ Desktop shortcut


✓ Start Menu shortcut


✓ Uninstaller registration


✓ Configuration preservation


✓ Upgrade installation


✓ Complete removal



---

# Installation Wizard Design


The installation process:


```

Welcome


↓

License / Information


↓

Installation Location


↓

Shortcut Options


↓

Install


↓

Finish


```



---

# Welcome Page


Display:


```

Welcome to LeslieCleaner Setup


Install LeslieCleaner on your computer.


```



---

# Installation Location Page


Critical requirement:


User can choose installation directory.



---

# Default Path


Recommended:


```
C:\Program Files\LeslieCleaner

```



---

# Custom Path Example


User can select:


```
D:\Software\LeslieCleaner


E:\Tools\LeslieCleaner


```



---

# Installation Directory Rules


Path must:


Exist or create


↓

Have write permission


↓

Have enough space



---

# Invalid Path Handling


Reject:


```

System system folders


↓

Root directory


↓

Protected locations


```



---

# Installation Permission Model


LeslieCleaner requires:


Administrator permission



Reason:


Need access:


Registry


↓

System Services


↓

Startup Management


↓

System Files



---

# Permission Flow


```

Start Installer


↓

Request Administrator Permission


↓

Verify Permission


↓

Continue Installation


```



---

# Installed File Structure


Recommended:


```
LeslieCleaner


│

├── LeslieCleaner.exe


├── Core


├── Modules


├── Resources


├── Config


├── Database


├── Logs


├── Backup


└── Plugins


```



---

# Directory Explanation


## Core


Contains:


Main runtime components.



---

## Modules


Contains:


Functional modules.


Example:


```
Cleaner

Registry

Startup

Uninstaller


```



---

## Resources


Contains:


```

Icons

Images

Language Files


```



---

## Config


Contains:


User configuration.



---

## Database


Contains:


SQLite database.



Example:


```
LeslieCleaner.db

```



---

## Logs


Contains:


Runtime logs.



---

## Backup


Contains:


Registry backup


↓

Cleanup backup


↓

Restore files



---

## Plugins


Contains:


Optional extensions.



---

# User Data Separation


Important:


Program files and user data should be separated.



---

# Recommended


Program:


```
C:\Program Files\LeslieCleaner

```



User Data:


```
C:\ProgramData\LeslieCleaner

```



---

# User Data Includes


```

Database


↓

Logs


↓

Backup


↓

Configuration


```



---

# Upgrade Strategy


When installing new version:


Do not delete:


```

Database


↓

Backup


↓

User Settings


```



---

# Upgrade Process


```

Detect Existing Installation


↓

Backup Configuration


↓

Replace Program Files


↓

Run Database Migration


↓

Verify


```



---

# Version Storage


Store:


```
ApplicationVersion


DatabaseVersion


```



---

# Installation Verification


After installation:


Check:


Executable exists


↓

Dependencies available


↓

Database initialized


↓

Configuration created


↓

Shortcut created



---

# Shortcut Creation


Options:


Desktop Shortcut


↓

Start Menu Shortcut



---

# Shortcut Properties


Include:


```

Name:

LeslieCleaner


Target:

LeslieCleaner.exe


Icon:

LeslieCleaner.ico


```



---

# First Launch Process


When first running:


```

Check Environment


↓

Check Permission


↓

Initialize Database


↓

Load Configuration


↓

Start Application


```



---

# Environment Check


Verify:


.NET Runtime


↓

System Version


↓

Required Permission


↓

Disk Space



---

# Missing Dependency Handling


If missing:


Show:


```

Required component is missing.

Please install required runtime.


```



---

# Uninstaller Design


LeslieCleaner must provide complete uninstall.



---

# Uninstall Entry


Register in:


System Apps & Features



---

# Uninstall Options


User chooses:


```

Remove Program Only


or


Remove Program + Data


```



---

# Program Only


Remove:


```

Executable

Modules

Resources


```



---

# Program + Data


Remove:


```

Database

Logs

Backup

Configuration


```



---

# Safety Confirmation


Before removing data:


Show:


```

This will permanently remove LeslieCleaner data.

Continue?


```



---

# Uninstall Process


```

Stop Application


↓

Stop Background Services


↓

Remove Files


↓

Remove Registry Entries


↓

Remove Shortcuts


↓

Cleanup


```



---

# Important


Uninstaller must not remove:


User personal files.



---

# Installation Logging


Installer records:


```

Installation Time


Install Path


Version


Result


```



---

# Deployment Final Requirement


The installation system must make LeslieCleaner:


Easy to install


↓

Easy to update


↓

Easy to remove


↓

Safe for long-term personal use


---
---

# Background Component Deployment


LeslieCleaner should minimize background processes.


Principle:


Do not create unnecessary System services.



---

# Background Running Mode


Preferred:


User-level background task



Avoid:


Permanent System Service


unless required.



---

# Background Task Usage


Suitable for:


Scheduled cleanup reminder


↓

Update checking


↓

Health monitoring



---

# Background Task Restrictions


Must:


Low CPU usage


↓

Low memory usage


↓

User controllable



---

# Background Task Settings


User can configure:


```

Enable background monitoring

□


Start with System

□


Run scheduled scan

□


```



---

# Startup Registration Deployment


LeslieCleaner startup behavior must be transparent.



---

# Startup Entry Rules


If enabled:


Create startup entry.



If disabled:


Remove startup entry.



---

# Startup Registration Location


Recommended:


System Startup Manager API


or


Registry:



```



---

# Startup Permission


Default:


Disabled.



User must manually enable.



---

# First Installation


Do not automatically add startup.



---

# Startup Optimization Relationship


LeslieCleaner itself should appear as:


User Application Startup



Not:


System Startup.



---

# Configuration Migration


Purpose:


Preserve user settings during upgrade.



---

# Migration Data


Include:


```

Application Settings


↓

Scan Preferences


↓

Whitelist


↓

Blacklist


↓

UI Preferences


↓

Scheduled Tasks


```



---

# Migration Process


```

Detect Old Version


↓

Read Configuration


↓

Convert Format


↓

Save New Format


↓

Verify


```



---

# Configuration Versioning


Example:


```
ConfigVersion = 1


```



Future:


```
ConfigVersion = 2


```



Migration required when:


Version changes.



---

# Database Migration


SQLite database requires:


Migration mechanism.



---

# Database Upgrade Process


```

Backup Database


↓

Execute Migration


↓

Verify Tables


↓

Update Version


```



---

# Migration Failure Handling


If failed:


Restore backup database.



---

# Backup System Deployment


LeslieCleaner must provide recovery capability.



---

# Backup Types


## Registry Backup


Before:


Registry modification.



---

## Cleanup Backup


Before:


Important file deletion.



---

## Configuration Backup


Before:


Upgrade.



---

# Backup Directory


Example:


```

ProgramData

 └── LeslieCleaner

       └── Backup


```



---

# Backup Naming


Format:


```

Backup_YYYYMMDD_HHMMSS


```



Example:


```

Backup_20260726_153000


```



---

# Backup Retention


User configurable.



Default:


Keep latest 10 backups.



---

# Restore Process


```

Select Backup


↓

Verify Backup


↓

Confirm Restore


↓

Execute


↓

Restart if needed


```



---

# Installation Failure Recovery


The installer must handle:


Insufficient space


↓

Permission failure


↓

File lock


↓

Interrupted installation



---

# Rollback Strategy


If installation fails:


Restore:


```

Previous Files


↓

Configuration


↓

Database


```



---

# File Lock Handling


If application running:


Show:


```

LeslieCleaner is running.

Please close it before continuing.


```



---

# Self Repair Mechanism


Optional feature.


Purpose:


Repair damaged installation.



---

# Repair Function


Can restore:


```

Missing Program Files


↓

Broken Configuration


↓

Missing Resources


```



---

# Repair Process


```

Detect Installation


↓

Compare Files


↓

Restore Missing Files


↓

Verify


```



---

# Local Update Mechanism


Because LeslieCleaner is personal software:


No public update server required.



---

# Update Sources


Support:


Local package


↓

Manual update


↓

Private storage



---

# Update Package Structure


Example:


```

Update.zip


|

├── Application Files

├── Version.json

└── Migration Scripts


```



---

# Update Process


```

Check Version


↓

Backup Current Version


↓

Close Application


↓

Replace Files


↓

Migration


↓

Restart


```



---

# Version File


Example:


```
{
 "Version":"1.0.0",
 "BuildDate":"2026-07-26"
}

```



---

# Deployment Build Process


Development:


```

Debug Build


↓

Testing


↓

Release Build


↓

Package


```



---

# Release Build Optimization


Enable:


```

Optimize Code


↓

Remove Debug Symbols


↓

Enable Compiler Optimization


```



---

# Build Output Structure


Example:


```

Build


|

├── LeslieCleaner.exe

├── Dependencies

├── Resources

└── Config


```



---

# Portable Mode (Optional)


Support optional portable operation.



---

# Portable Mode Rules


Store:


```

Database

↓

Config

↓

Logs


```


inside:


```
.\Data

```



---

# Portable Mode Restrictions


Do not:


Modify system automatically.



---

# Installer Security


Installer should verify:


File integrity


↓

Hash


↓

Source validity



---

# Deployment Cleanup


Before packaging:


Remove:


```

Debug Files


↓

Temporary Files


↓

Test Data


↓

Unused Resources


```



---

# Final Deployment Checklist


Before installation package creation:


□ Build successful


□ Release configuration


□ Dependencies checked


□ Installer tested


□ Custom path tested


□ Upgrade tested


□ Uninstall tested


□ Data preservation tested


□ Rollback tested



---

# Final Deployment Principle


LeslieCleaner installation must provide:


Professional installation experience


while remaining simple and suitable for personal long-term usage.



END OF DOCUMENT