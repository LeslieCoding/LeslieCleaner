# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Cleaner Engine Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture of the LeslieCleaner core cleaning engine.


The cleaner engine is responsible for:


System junk detection

↓

Temporary file analysis

↓

Cache detection

↓

Log detection

↓

Residual file detection

↓

Safe cleanup execution



---

# Design Goal


The cleaning engine must achieve:


High accuracy


↓

High safety


↓

Fast scanning


↓

Low memory usage


↓

Recoverable operation



---

# Core Design Principle


LeslieCleaner does not blindly delete files.


Every cleanup operation requires:


Detection


↓

Analysis


↓

Risk Evaluation


↓

User Confirmation


↓

Backup


↓

Cleanup



---

# Architecture Overview


```

Cleaner Engine


        |

        |

+----------------+

Scanner Layer

+----------------+

        |

        |

+----------------+

Analyzer Layer

+----------------+

        |

        |

+----------------+

Risk Engine

+----------------+

        |

        |

+----------------+

Cleanup Executor

+----------------+

        |

        |

+----------------+

Backup System

+----------------+


```



---

# Module Structure


```

Cleaner


|

├── Scanner


├── Analyzer


├── Rules


├── Risk


├── Executor


├── Backup


├── Report


```



---

# Scanner Layer


Responsibility:


Find possible cleanup targets.



---

# Scanner Types



## Temporary File Scanner


Detect:


```

System Temp


User Temp


Application Temp


Browser Cache


```



---

## Cache Scanner


Detect:


```

Browser Cache


Application Cache


Thumbnail Cache


Update Cache


```



---

## Log Scanner


Detect:


```

Application Logs


Installation Logs


Crash Logs


```



---

## Residual Scanner


Detect:


```

Uninstalled Application Residual Files


Old Configuration Files


Unused Resources


```



---

# Scanner Interface


Every scanner must implement:



```csharp

public interface ICleanerScanner

{

    Task<IEnumerable<ScanItem>> ScanAsync(
        CancellationToken token);

}


```



---

# ScanItem Model


Contains:


```

Id


Name


Path


Size


Category


RiskLevel


CanDelete


```



---

# Scanner Requirements


Must support:


Async


↓

Cancellation


↓

Progress Reporting


↓

Exception Handling



---

# Analyzer Layer


Purpose:


Determine whether files are safe to remove.



---

# Analyzer Responsibilities


Analyze:


File Type


↓

File Age


↓

File Usage


↓

Application Relationship


↓

System Importance



---

# Example Analysis


File:


```

Chrome Cache


```



Analyzer:


Check:


Is cache file?


↓

Is browser running?


↓

Is user protecting data?


↓

Can remove?



---

# Risk Engine


Purpose:


Calculate cleanup risk.



---

# Risk Levels


```

Safe


Low Risk


Medium Risk


High Risk


Critical


```



---

# Risk Calculation Factors


Include:


File Location


↓

File Type


↓

System Importance


↓

Application Dependency


↓

User History



---

# Risk Example


System Temporary File:


```

Risk:

Safe


```



System Driver File:


```

Risk:

Critical


```
---

# Cleanup Executor


## Purpose


Execute actual cleanup operations safely.



---

# Executor Responsibilities


Handle:


File deletion


↓

Directory cleanup


↓

Cache removal


↓

Temporary data removal



---

# Executor Interface


```csharp

public interface ICleanupExecutor

{

    Task<CleanupResult> ExecuteAsync(

        IEnumerable<ScanItem> items,

        CancellationToken token);

}

```



---

# CleanupResult Model


Contains:


```

TotalItems


SuccessCount


FailedCount


ReleasedSize


Errors


```



---

# Cleanup Execution Flow


```

Receive Selected Items


↓

Validate Items


↓

Check Protection Rules


↓

Create Backup


↓

Execute Cleanup


↓

Verify Result


↓

Generate Report


```



---

# Safety Validation


Before deletion:


Check:


```

File Exists


↓

Permission


↓

Protected Path


↓

Risk Level


↓

User Confirmation


```



---

# Protected Path System


The cleaner must maintain a protection list.



---

# Protected Locations


Never delete automatically:


```

Boot Related Files


Driver Files


```



---

# Protected File Types


Avoid:


```

.sys


.dll


.exe


.drv


```



unless explicitly analyzed.



---

# WhiteList System


Purpose:


Prevent accidental deletion.



---

# Whitelist Types


## System Whitelist


Built-in protection.



---

## User Whitelist


User-defined protection.



Example:


```

D:\ImportantData


```



---

# Whitelist Check Flow


```

Scan Item


↓

Check System Whitelist


↓

Check User Whitelist


↓

Allow / Block


```



---

# Blacklist System


Purpose:


Never touch dangerous locations.



---

# Example:


```

System Boot Files


Security Software Files


Hardware Drivers


```



---

# Backup System Integration


Before deleting important items:


Create backup.



---

# Backup Types



## Lightweight Backup


Store:


Metadata


↓

File Path


↓

Original Information



---

## Full Backup


Store:


Original File



---

# Backup Strategy


Default:


Only backup risky operations.



---

# Example:


Temporary Cache:


No backup required.



Registry Related:


Backup required.



---

# Restore Capability


User can:


View backup


↓

Select restore


↓

Recover



---

# Delete Strategy


Different files use different strategies.



---

# Normal Delete


Used for:


Temporary files



Method:


Standard filesystem delete.



---

# Recycle Bin Delete


Used for:


User-visible files.



---

# Permanent Delete


Only for:


File Shredder module.



Not default cleaner behavior.



---

# Empty Folder Cleanup


After deleting files:


Check empty folders.



---

# Empty Folder Rule


Delete only if:


Folder contains no useful data.



---

# Multi-thread Scan Architecture


Scanner supports parallel execution.



---

# Architecture


```

Directory Queue


↓

Worker Tasks


↓

Scanner


↓

Result Collector


```



---

# Thread Control


Use:


```

SemaphoreSlim


```



---

# Maximum Workers


Recommended:


```

CPU Core Count × 2


```



---

# Memory Optimization


Scanner must:


Avoid loading all results.



---

# Use:


```

IAsyncEnumerable


```



---

# Bad:


```

List<ScanItem>


Millions of items


```



---

# Good:


```

Stream Results


↓

Process Incrementally


```



---

# Progress Reporting


Every scan operation reports:



```

Current Category


Current Path


Processed Count


Found Size


Percentage


```



---

# Cancellation Support


User can stop scanning.



---

# Cancellation Flow


```

User Click Stop


↓

CancellationToken Trigger


↓

Stop Workers


↓

Save Current State


```



---

# Performance Optimization


## Disk Optimization


Avoid:


Repeated disk scanning.



---

# Use:


Cache scan results.



---

# Hash Optimization


Small files:


Full hash.



Large files:


Partial hash first.



---

# Duplicate Detection Integration


Cleaner engine can provide:


Duplicate file candidates.



---

# Duplicate Detection Process


```

File Size


↓

Partial Hash


↓

Full Hash


↓

Compare


```



---

# Reporting System


After cleanup:


Generate report.



---

# Report Contains:


```

Scan Time


↓

Cleaned Items


↓

Released Space


↓

Skipped Items


↓

Errors


```



---

# Dashboard Integration


Cleaner results provide:


```

Total Cleaned Size


↓

History Statistics


↓

Health Score


```



---

# Cleaner Engine Final Requirement


The cleaning engine must be:


Safe enough for beginners.


↓

Powerful enough for advanced users.


↓

Fast enough for daily usage.


↓

Stable enough for long-term operation.



END OF DOCUMENT


---