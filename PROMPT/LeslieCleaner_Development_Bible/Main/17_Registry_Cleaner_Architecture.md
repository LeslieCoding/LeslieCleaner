# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Registry Cleaner Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner Registry Cleaner module.


The registry cleaner is responsible for:


Registry analysis

↓

Invalid entry detection

↓

Risk evaluation

↓

Backup creation

↓

Safe repair

↓

Restore operation



---

# Core Design Philosophy


Registry cleaning is a high-risk operation.


Therefore:


Safety > Cleaning Amount



The system must never aggressively delete registry entries just to increase cleaning results.



---

# Main Goals


The registry module must provide:


Accurate detection


↓

Low false-positive rate


↓

Complete backup


↓

Reliable restore


↓

Transparent user control



---

# Architecture Overview


```

Registry Cleaner


        |

        |

+----------------+

Registry Scanner

+----------------+

        |

        |

+----------------+

Rule Analyzer

+----------------+

        |

        |

+----------------+

Risk Engine

+----------------+

        |

        |

+----------------+

Backup Manager

+----------------+

        |

        |

+----------------+

Repair Executor

+----------------+



```



---

# Module Structure


```

Registry


|

├── Scanner


├── Providers


├── Rules


├── Analyzer


├── Backup


├── Executor


├── Restore


├── Report


```



---

# Registry Provider Layer


Purpose:


Provide safe System Registry access.



---

# Provider Responsibilities


Handle:


Registry reading


↓

Registry enumeration


↓

Registry backup


↓

Registry modification



---

# Registry API Abstraction


Never directly call System Registry APIs everywhere.



---

# Required Interface



```csharp

public interface IRegistryProvider

{

    Task<IEnumerable<RegistryEntry>> ScanAsync();


    Task BackupAsync();


    Task RestoreAsync();

}

```



---

# Supported Registry Areas


Main:


```

HKEY_CURRENT_USER


HKEY_LOCAL_MACHINE


HKEY_CLASSES_ROOT


```



---

# Protected Registry Areas


Never automatically modify:


```

System Boot Configuration


System Drivers


Security Configuration


Hardware Information


```



---

# Registry Scanner


Purpose:


Find potentially invalid registry entries.



---

# Scanner Categories


Include:



## Invalid Application Paths


Detect:


Programs pointing to missing files.



Example:


```

C:\OldSoftware\App.exe


```



---

## Uninstalled Software Residuals


Detect:


Remaining keys after uninstall.



Example:


```

Old Application Name


Old Publisher


```



---

## Broken File Associations


Detect:


File extensions pointing to unavailable applications.



Example:


```

.txt


→

Missing Program


```



---

## Invalid Startup Registry


Detect:


Startup entries pointing to deleted programs.



---

## Empty Registry Entries


Detect:


Unused empty keys.



---

# Scanner Interface


```csharp

public interface IRegistryScanner

{

    Task<IEnumerable<RegistryIssue>>

    ScanAsync(

    CancellationToken token);


}

```



---

# RegistryIssue Model


Contains:


```

Id


RegistryPath


KeyName


Value


Category


RiskLevel


Recommendation


CanRepair


```



---

# Registry Rule Engine


Purpose:


Evaluate registry entries.



---

# Rule Structure


Every rule contains:


```

Condition


↓

Analysis


↓

Risk


↓

Action


```



---

# Example Rule


Invalid Path Rule:



Condition:


File does not exist.



Analysis:


Registry points to missing executable.



Risk:


Low.



Action:


Recommend removal.



---

# Risk Evaluation System


Registry items receive risk scores.



---

# Risk Factors


Consider:


Registry Location


↓

Entry Type


↓

System Dependency


↓

Application Relationship


↓

User Impact



---

# Risk Levels



## Safe


Example:


Old temporary application entries.



---

## Low Risk


Example:


Missing software paths.



---

## Medium Risk


Example:


Unused file associations.



---

## High Risk


Example:


Startup-related entries.



---

## Critical


Example:


System configuration entries.



---

# Automatic Repair Rules


Only:


Safe


↓

Low Risk


items can be automatically repaired.



---

# Medium Risk


Require:


User confirmation.



---

# High / Critical


Default:


Do not modify.



---

# Registry Backup System


Every modification requires backup.



---

# Backup Process


```

Select Registry Items


↓

Export Original Data


↓

Save Backup File


↓

Record Database Information


↓

Execute Modification


```



---

# Backup Format


Recommended:


```

.reg


```



---

# Backup Storage


Example:


```

ProgramData

└── LeslieCleaner

    └── Backup

        └── Registry


```



---

# Backup Naming


Format:


```

RegistryBackup_Date_Time.reg


```



---

# Restore System


Purpose:


Allow user recovery.



---

# Restore Flow


```

Select Backup


↓

Verify Backup


↓

Confirm Restore


↓

Import Registry


↓

Restart if Required


```



---

# Restore Safety


Before restore:


Create current backup.



---

# Registry Modification Executor


Responsible:


Execute approved changes.



---

# Executor Rules


Before modification:


1.

Check backup exists


2.

Check permission


3.

Check risk level


4.

Log operation


5.

Execute


6.

Verify result



---

# Operation Logging


Every registry change records:


```

Time


User Action


Registry Path


Old Value


New Value


Result


```



---

# User Interface Integration


Registry module must provide:


Scan progress


↓

Issue list


↓

Risk explanation


↓

Backup status


↓

Repair result



---

# Registry Cleaner Final Principle


A professional registry cleaner is not the one that deletes the most entries.


It is the one that:

Finds meaningful problems.

↓

Explains risks clearly.

↓

Changes safely.

↓

Can always recover.



---
---

# Registry Snapshot System


## Purpose


Provide a complete state record before risky operations.



---

# Snapshot Usage


Used for:


Major registry repair


↓

Batch modification


↓

System optimization operations



---

# Snapshot Creation Flow


```

User Starts Repair


↓

Analyze Selected Entries


↓

Create Snapshot


↓

Verify Snapshot


↓

Allow Modification


```



---

# Snapshot Content


Contains:


```

Registry Path


↓

Key Name


↓

Original Value


↓

Value Type


↓

Timestamp


↓

Operation Source


```



---

# Snapshot Storage


Recommended:


SQLite database + .reg backup file.



---

# Snapshot Database Relationship


Relationship:


```

RegistryBackups


        |

        |

RegistryBackupItems


```



---

# RegistryBackupItems Table


Structure:


```

Id


BackupId


RegistryPath


ValueName


OldValue


ValueType


CreatedTime


```



---

# Registry Scan Optimization


Registry scanning must be fast.



---

# Optimization Methods


Use:


Selective scanning


↓

Rule-based detection


↓

Cache


↓

Incremental analysis



---

# Avoid Full Registry Blind Scan


Forbidden:


Scan all registry keys every time.



---

# Correct Approach:


```

Known Risk Locations


↓

Targeted Rules


↓

Analyze Relevant Entries


```



---

# Registry Cache System


Purpose:


Avoid repeated analysis.



---

# Cache Content


Store:


```

Registry Path


Last Result


Risk Level


Scan Time


```



---

# Cache Expiration


Recommended:


7 days.



---

# Incremental Scan


If:


Registry unchanged.



Then:


Reuse previous analysis.



---

# False Positive Prevention


Critical requirement.



---

# Detection Confirmation


Before marking invalid:



Check:


```

Path Exists


↓

Application Exists


↓

File Signature


↓

Publisher Information


↓

Recent Usage


```



---

# Example


Registry:


```

OldApp.exe


```



Cannot immediately delete.



Need:


Check file existence.



---

# Publisher Verification


Use:


Digital Signature


↓

File Metadata


↓

Installation Records



---

# Application Relationship Analysis


Connect registry entries with:


Applications table.



---

# Example


Registry:


```

Steam Startup Entry


```



Check:


Applications table:


```

Steam Installed


```



Decision:


Keep.



---

# Repair Validation System


After repair:


Verify result.



---

# Validation Steps


```

Execute Modification


↓

Read Registry Again


↓

Confirm Change


↓

Generate Report


```



---

# Failed Repair Handling


If failed:


```

Stop Operation


↓

Restore Backup


↓

Record Error


```



---

# Registry Repair Report


Contains:


```

Repair Time


↓

Modified Entries


↓

Successful Count


↓

Failed Count


↓

Restoration Information


```



---

# User Confirmation System


Different risk levels require different confirmation.



---

# Safe


Automatic option:



```

Allow One Click Repair


```



---

# Low Risk


Default:


Show confirmation.



---

# Medium Risk


Require:


Detailed explanation.



---

# High Risk


Require:


Advanced confirmation.



---

# Critical


Not automatically repairable.



---

# Batch Registry Repair


Support:


```

Select All


Select None


Invert Selection


Batch Repair


```



---

# Batch Safety


Before batch operation:


Check every item individually.



---

# Registry Module Performance Targets


Scan:


<10 seconds


for normal systems.



---

# Memory Usage


Registry scan:


<100MB additional memory.



---

# Thread Model


Use:


Async Task



Avoid:


Blocking registry operations.



---

# Exception Handling


Handle:


```

Access Denied


↓

Invalid Key


↓

Registry Locked


↓

Permission Failure


```



---

# Logging Requirements


Record:


```

Scan Started


Scan Completed


Repair Started


Repair Completed


Failure


Restore


```



---

# Security Restrictions


Registry cleaner must not:


Modify security settings.


↓

Disable antivirus.


↓

Change System protection.


↓

Modify boot configuration.



---

# Testing Requirements


Must test:



## Normal Registry


Common software entries.



---

## Broken Entries


Missing applications.



---

## Protected Entries


System critical keys.



---

## Permission


Standard user.


↓

Administrator.



---

# Final Registry Module Requirement


The Registry Cleaner must be:


Safe


↓

Transparent


↓

Recoverable


↓

Accurate


↓

Performance Optimized



END OF DOCUMENT