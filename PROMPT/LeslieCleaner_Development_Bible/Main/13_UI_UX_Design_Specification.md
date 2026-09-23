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