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