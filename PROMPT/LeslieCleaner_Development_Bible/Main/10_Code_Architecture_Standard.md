# LeslieCleaner Development Bible

Version

1.0

Document Type

Code Architecture Standard

Priority

★★★★★


---

# Purpose

This document defines the complete software engineering standards for LeslieCleaner.

It controls:

Project Structure

↓

Code Organization

↓

Architecture Pattern

↓

Class Design

↓

Dependency Management

↓

Coding Style

↓

Testing Standard

↓

AI Code Generation Rules


All generated code must comply with this document.

---

# Core Development Philosophy


LeslieCleaner must be:


Maintainable

↓

Readable

↓

Extensible

↓

Testable

↓

Performance Optimized

↓

Secure


---

# Recommended Technology Architecture


Desktop Application:


Recommended:


```
.NET 8

+

C#

+

WPF

+

MVVM

+

SQLite

+

  API

```


---

# Architecture Pattern


Use:


## MVVM + Clean Architecture


Architecture layers:


```

Presentation Layer


↓

Application Layer


↓

Domain Layer


↓

Infrastructure Layer


↓

Platform Layer


```


---

# Layer Responsibilities


## Presentation Layer


Responsible:


UI

↓

View

↓

ViewModel

↓

User Interaction


---

Forbidden:


Business Logic


↓

Database Access


↓

  API


---

# Application Layer


Responsible:


Application workflows.


Examples:


Uninstall Application


↓

Execute Cleanup


↓

Analyze Startup


---

Contains:


Application Services


↓

DTO


↓

Commands


↓

Queries


---

# Domain Layer


Responsible:


Core business rules.


Examples:


Cleaning Rules


↓

Risk Evaluation


↓

Fingerprint Matching


---

Should not depend on:


UI

↓

Database

↓

  API


---

# Infrastructure Layer


Responsible:


External implementation.


Examples:


SQLite


↓

File System


↓

Registry


↓

Network


---

# Platform Layer


Responsible:


  specific functions.


Examples:


  Registry API


↓

Service Manager API


↓

Scheduled Task API


↓

  Event API


---

# Project Structure Standard


Solution:


```
LeslieCleaner.sln

```


---

# Main Projects


```
LeslieCleaner.UI


LeslieCleaner.Application


LeslieCleaner.Domain


LeslieCleaner.Infrastructure


LeslieCleaner.Platform


LeslieCleaner.Tests


```


---

# UI Project Structure


```
LeslieCleaner.UI

│

├── Views

├── ViewModels

├── Controls

├── Resources

├── Styles

├── Converters

├── Themes


```


---

# Views


Contains:


Window


↓

Page


↓

UserControl


---

Example:


```
CleanerView.xaml

RegistryView.xaml

StartupView.xaml

```


---

# ViewModels


Each View requires:


Corresponding ViewModel.


Example:


```
CleanerViewModel.cs

RegistryViewModel.cs

StartupViewModel.cs

```


---

# Controls


Reusable components.


Examples:


```
ProgressCard

StatusCard

ScanResultList

ModernButton

```


---

# Resources


Contains:


Images


↓

Icons


↓

Localization


↓

Fonts


---

# Styles


Contains:


```
ButtonStyles

CardStyles

TextStyles

AnimationStyles

```


---

# Application Project Structure


```
LeslieCleaner.Application


├── Services

├── Interfaces

├── DTOs

├── Commands

├── Queries

├── Validators


```


---

# Services Folder


Contains:


Application service implementations.


Example:


```
CleanerService.cs

UninstallerService.cs

StartupService.cs


```


---

# Interfaces Folder


Contains:


Contracts.


Example:


```
ICleanerService.cs

IStartupService.cs

IRegistryService.cs


```


---

# DTO Folder


Contains:


Data Transfer Objects.


Example:


```
ApplicationInfoDto.cs

ScanResultDto.cs

CleanupResultDto.cs


```


---

# Domain Project Structure


```
LeslieCleaner.Domain


├── Entities

├── ValueObjects

├── Rules

├── Algorithms

├── Exceptions


```


---

# Entities


Core objects.


Example:


```
Application

StartupItem

CleanRule

Fingerprint


```


---

# ValueObjects


Objects without identity.


Example:


```
FileSize

RiskScore

Version


```


---

# Rules


Business rules.


Example:


```
RegistryRiskRule

CleanupSafetyRule


```


---

# Algorithms


Core algorithms.


Example:


```
FingerprintMatcher

RiskCalculator

DuplicateDetector


```


---

# Infrastructure Project Structure


```
LeslieCleaner.Infrastructure


├── Database

├── Repositories

├── FileSystem

├── Logging

├── Configuration


```


---

# Database Folder


Contains:


```
SQLiteContext

Migration

DatabaseInitializer


```


---

# Repository Pattern


Example:


Interface:


```
IApplicationRepository


```


Implementation:


```
ApplicationRepository


```


---

# FileSystem Folder


Contains:


```
FileScanner

FileAnalyzer

FileOperator


```


---

# Logging Folder


Contains:


```
Logger

LogWriter

LogFormatter


```


---

# Platform Project Structure


```
LeslieCleaner.Platform


├── Registry

├──  Services

├── Startup

├── Process

├── Security


```


---

# Registry Folder


Contains:


```
RegistryScanner

RegistryBackup

RegistryOperator


```


---

#  Services Folder


Contains:


```
ServiceScanner

ServiceManager


```


---

# Startup Folder


Contains:


```
StartupScanner

StartupClassifier

StartupOptimizer


```


---

# Dependency Rule


Allowed:


```
UI

 ↓

Application

 ↓

Domain


Infrastructure

 ↓

Domain


Platform

 ↓

Domain


```


---

Forbidden:


```
Domain

↓

Infrastructure


```


---

# Dependency Injection Standard


Use:


Microsoft.Extensions.DependencyInjection


---

# Service Registration


Example:


```
services.AddSingleton<ICleanerService,
CleanerService>();

```


---

# Lifetime Rules


Singleton:


For:


Database

↓

Configuration

↓

Logger


---

Scoped:


For:


Operation Context


---

Transient:


For:


Temporary Services


---

# Class Design Principles


Every class should:


Have one responsibility.


---

Avoid:


God Class


Example:


```
SystemManager.cs

```


containing:


Cleaner

↓

Registry

↓

Startup

↓

Uninstaller


---

# Prefer:


```
CleanerService

RegistryService

StartupService

```


---

# Naming Standard


Classes:


PascalCase


Example:


```
CleanScanner


```


---

Methods:


PascalCase


Example:


```
ScanFilesAsync()


```


---

Variables:


camelCase


Example:


```
scanResult


```


---

Constants:


UPPER_CASE


Example:


```
MAX_SCAN_THREADS


```


---
---

# Async Programming Standard

## Purpose

LeslieCleaner contains many time-consuming operations:

File scanning

↓

Registry analysis

↓

Application detection

↓

Residual search

↓

Hash calculation

↓

Database processing


All long operations must be asynchronous.


---

# Mandatory Async Rule


Forbidden:


```
void ScanFiles()

{

// Long operation

}

```


---

Required:


```
Task<ScanResult> ScanFilesAsync()

{

}

```


---

# Async Naming Standard


All asynchronous methods must end with:


```
Async

```


Example:


```
CleanAsync()

AnalyzeAsync()

ScanAsync()

```


---

# UI Thread Protection


Never block UI thread.


Forbidden:


```
Task.Wait()

Result


```


---

Use:


```
await

```


---

# Cancellation Support


All long operations must support:


```
CancellationToken

```


Example:


```
Task ScanAsync(
CancellationToken cancellationToken)

```


---

# Progress Reporting


Long operations must report progress.


Use:


```
IProgress<T>

```


Example:


```
IProgress<ScanProgress>

```


---

# Operation State Model


Every long task has:


```
Created

↓

Running

↓

Paused

↓

Completed

↓

Cancelled

↓

Failed


```


---

# Multi-threading Architecture


LeslieCleaner uses:


Task Parallel Library


+

Controlled Thread Pool


---

# Thread Usage Rules


Never create unlimited threads.


Forbidden:


```
new Thread()

inside loop


```


---

Use:


```
Task.Run()

↓

Parallel

↓

SemaphoreSlim


```


---

# Scan Parallel Architecture


Example:


```
File Scanner


        ↓


Directory Queue


        ↓


Worker Tasks


        ↓


Result Collector


```


---

# Concurrency Control


Every parallel operation requires:


```
SemaphoreSlim

```


Example:


Maximum:


```
CPU cores × 2


```


---

# CPU Optimization


Detect:


```
Environment.ProcessorCount


```


Adjust:


```
Worker Count


```


---

# IO Optimization


Disk operations are limited by:


Disk Type


↓

System Load


↓

Current Usage


---

# SSD Strategy


Use:


More parallel reading


↓

Higher concurrency


---

# HDD Strategy


Use:


Sequential scanning


↓

Lower concurrency


---

# Memory Optimization Coding Rules


## Principle


Avoid unnecessary allocation.


---

# Forbidden


Large temporary collections.


Example:


```
List<FileInfo>

10 million items


```


---

# Recommended


Streaming:


```
IEnumerable<FileInfo>


```


or:


```
IAsyncEnumerable<FileInfo>


```


---

# Object Lifetime Management


Large objects:


Must release quickly.


Example:


```
using()

{

LargeResource

}


```


---

# IDisposable Standard


Classes holding:


File Handles


↓

Database Connections


↓

Streams


↓

Native Resources


Must implement:


```
IDisposable


```


---

# Memory Leak Prevention


Monitor:


```
Object Count

↓

GC Memory

↓

Handle Count

↓

Thread Count


```


---

# Cache Design Standard


Every cache requires:


Maximum Size


↓

Expiration Time


↓

Cleanup Strategy


---

# Example:


```
ScanCache


Max:

200MB


Expire:

7 days


```


---

# File Processing Standard


## File Scanner


Must support:


Large Directory


↓

Permission Error


↓

Locked File


↓

Symbolic Link


---

# File Access Rules


Before accessing:


Check:


Existence


↓

Permission


↓

File Status


---

# Large File Processing


Never:


Load entire file.


Forbidden:


```
byte[] data =
File.ReadAllBytes(path)


```


---

Use:


```
Stream


BufferedStream


```


---

# Hash Calculation Standard


Used for:


Duplicate Detection


↓

Fingerprint


↓

Verification


---

# Hash Strategy


Small File:


Full Hash


---

Large File:


Partial Hash


↓

Full Hash if required


---

# File Deletion Safety


Before delete:


Validate:


```
Path

Owner

FileType

Risk


```


---

# Protected Path List


Never delete automatically:


```
 

System32

Program Files

Drivers

Boot


```


---

# Registry API Standard


Registry operations must use:


Dedicated service.


---

Forbidden:


UI:


↓

Registry API


---

Correct:


```
UI

↓

RegistryService

↓

RegistryProvider


```


---

# Registry Modification Rules


Before modification:


1.

Create Backup


2.

Validate Key


3.

Check Permission


4.

Execute


5.

Verify


---

#   API Wrapper Standard


Never call native API everywhere.


Create:


```
 ApiWrapper


```


---

Example:


```
I ProcessService


I RegistryService


I ServiceManager


```


---

# Exception Handling Architecture


## Principle


Never silently fail.


---

# Exception Layers


```

Platform Exception


↓

Infrastructure Exception


↓

Application Exception


↓

UI Notification


```


---

# Custom Exception Types


Create:


```
LeslieException


DatabaseException


FileAccessException


RegistryException


PermissionException


```


---

# Exception Logging


Every exception records:


```
Module

Method

Message

StackTrace

Time


```


---

# User Error Message


Do not show:


Technical stack trace.


---

Bad:


```
NullReferenceException

0x00023


```


---

Good:


```
Unable to clean this file.

The file is currently in use.


```


---

# Logging System Standard


Recommended:


Serilog


---

# Log Levels


```
Trace

Debug

Information

Warning

Error

Critical


```


---

# Logging Categories


```
Application

Cleaner

Registry

Uninstaller

Startup

Database

Security


```


---

# Log File Structure


```
Logs

|

├── Application.log

├── Error.log

├── Operation.log


```


---

# Log Rotation


Rules:


Maximum Size


↓

Archive


↓

Delete Old Logs


---

# Sensitive Data Protection


Never log:


Passwords


↓

Cookies


↓

Personal Files Content


↓

Registry Values containing secrets


---

# Configuration Management


Use:


```
IConfigurationService


```


---

# Configuration Rules


Support:


Read


↓

Update


↓

Validate


↓

Save


---

# Configuration Storage


Recommended:


JSON


+

Encrypted Sensitive Values


---

# Unit Testing Standard


Every important module requires tests.


---

# Test Projects


```
LeslieCleaner.Tests


├── DomainTests

├── ServiceTests

├── IntegrationTests


```


---

# Unit Test Requirements


Test:


Normal Case


↓

Boundary Case


↓

Error Case


↓

Performance Case


---

# Required Test Coverage


Core modules:


Minimum:


80%


---

# Modules Requiring High Coverage


Cleaner


↓

Registry


↓

Uninstaller


↓

Startup


↓

Backup


---

# Code Review Rules


Before merging:


Check:


Architecture


↓

Security


↓

Performance


↓

Memory


↓

Naming


---

# AI Code Generation Rules


When AI generates code:


Must follow:


1.

MVVM architecture


2.

Dependency Injection


3.

Async programming


4.

Interface first design


5.

No duplicated code


6.

No hardcoded paths


7.

No UI business logic


8.

Complete exception handling


9.

Complete logging


10.

Complete comments for complex algorithms


---

# AI Forbidden Code


AI must not generate:


```
static global state


↓

Thread.Sleep


↓

Blocking IO


↓

Direct database access in UI


↓

Hardcoded   paths


↓

Unsafe delete operations


```


---

# Final Code Architecture Requirement


All LeslieCleaner source code must be:


Professional

↓

Production Ready

↓

Maintainable

↓

Performance Optimized

↓

Secure


The generated code must be suitable for commercial software release.


END OF DOCUMENT