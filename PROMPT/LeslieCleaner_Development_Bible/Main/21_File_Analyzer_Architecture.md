# LeslieCleaner Development Bible

Version:

1.0


Document Type:

File Analyzer Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and design standards of the LeslieCleaner file analysis module.


The file analyzer is responsible for:


Disk analysis


↓

Large file detection


↓

Duplicate file detection


↓

Storage visualization


↓

Cleanup recommendation



---

# Core Design Philosophy


File analysis is not a simple file listing tool.


The system must understand:


Where space is used


↓

Why space is occupied


↓

Whether files are safe to remove



---

# Main Goals


The file analyzer must provide:


Fast scanning


↓

Accurate classification


↓

Low memory usage


↓

Safe recommendations


↓

Clear visualization



---

# Architecture Overview


```

File Analyzer


        |

        |

+----------------+

Directory Scanner

+----------------+

        |

        |

+----------------+

File Classifier

+----------------+

        |

        |

+----------------+

Hash Engine

+----------------+

        |

        |

+----------------+

Analysis Engine

+----------------+

        |

        |

+----------------+

Report Generator

+----------------+


```



---

# Module Structure


```

FileAnalyzer


|

├── Scanner


├── Classifier


├── Hash


├── DuplicateDetector


├── LargeFileDetector


├── Statistics


├── Report


```



---

# Directory Scanner


## Purpose


Collect file information from storage devices.



---

# Scanner Responsibilities


Analyze:


```

File Path


↓

File Size


↓

File Type


↓

Modified Time


↓

Created Time


↓

Access Time


```



---

# Scanner Interface


```csharp

public interface IFileScanner

{

    IAsyncEnumerable<FileInfo>

    ScanAsync(

        string path,

        CancellationToken token);


}

```



---

# File Information Model


```csharp

public class FileInfoModel

{

    public string Path { get; set; }


    public long Size { get; set; }


    public string Extension { get; set; }


    public DateTime ModifiedTime { get; set; }


    public FileCategory Category { get; set; }


}

```



---

# File Categories


The analyzer classifies files.



---

# System Files


Examples:


```

System


Drivers


System Components


```



---

# Application Files


Examples:


```

Installed Software


Game Files


Development Tools


```



---

# User Files


Examples:


```

Documents


Images


Videos


Projects


```



---

# Temporary Files


Examples:


```

Cache


Logs


Temporary Data


```



---

# Unknown Files


Files that cannot be confidently classified.



---

# Classification Rules


Use:


```

Path


↓

Extension


↓

Publisher


↓

Application Relationship


↓

File Metadata


```



---

# Protected Path System


The analyzer must protect:



```


System Reserved Areas


```



---

# Large File Detector


## Purpose


Find files consuming large storage.



---

# Default Threshold


Recommended:



```

>500MB


```



---

# User Configuration


Allow:


```

100MB


500MB


1GB


5GB


Custom


```



---

# Large File Information


Display:


```

File Name


↓

Path


↓

Size


↓

Last Modified


↓

Category


```



---

# Large File Risk Analysis


A large file is not automatically removable.



---

# Example:


Game Installation:


```

100GB


```



Recommendation:


Keep.



---

# Example:


Old ISO Backup:


```

20GB


```



Recommendation:


Review.



---

# Large File Recommendation Engine


Analyze:


```

File Age


↓

Usage Frequency


↓

File Type


↓

Location


```



---

# Recommendation Levels



## Safe Suggestion


Example:


Old temporary archive.



---

## Review Required


Example:


Large unknown file.



---

## Protected


Example:


System image file.



---

# Duplicate File Detector


## Purpose


Find identical files.



---

# Detection Strategy


Must avoid:


Full hashing all files immediately.



---

# Detection Pipeline


```

File Size Filtering


↓

Partial Hash


↓

Full Hash


↓

Duplicate Group


```



---

# Step 1

# Size Filtering


Only compare:


Files with same size.



---

# Step 2

# Partial Hash


Calculate:


File beginning


↓

File middle


↓

File ending



---

# Step 3

# Full Hash


For candidates:



Calculate complete hash.



---

# Supported Hash Algorithms


Recommended:


```

SHA-256


```



Optional:


```

MD5


```



---

# Hash Model


```

FileHash


Id


FilePath


FileSize


Algorithm


HashValue


CreatedTime


```



---

# Duplicate Group Model


```

DuplicateGroup


Id


Hash


TotalSize


FileCount


CreatedTime


```



---

# Duplicate Safety Rules


Never automatically delete duplicates.



---

# Reason:


Files may have:


Different purposes


↓

Different locations


↓

User importance



---

# Duplicate Recommendation


Show:


```

Possible duplicate


↓

Original file suggestion


↓

Space recoverable


```



---

# Duplicate Selection Logic


Prefer keeping:


```

Newest File


↓

User Folder


↓

Frequently Accessed File


```
---

# Disk Space Statistics System


## Purpose


Provide a clear overview of storage usage.



---

# Statistics Categories


Analyze:



```

System Usage


↓

Application Usage


↓

User Data


↓

Temporary Data


↓

Unknown Data


```



---

# Storage Analysis Model


```csharp

public class StorageStatistics

{

    public long TotalSize { get; set; }


    public long UsedSize { get; set; }


    public long FreeSize { get; set; }


    public long TemporarySize { get; set; }


    public long ApplicationSize { get; set; }


    public long UserDataSize { get; set; }

}

```



---

# Drive Scanner


Support:


```

C:


↓

D:


↓

External Drives


```



---

# Drive Information


Display:


```

Drive Name


↓

Capacity


↓

Used Space


↓

Free Space


↓

Health Status


```



---

# Storage Visualization


The UI should provide:



## Usage Ring Chart


Display:


```

Used


Free


Cleanable


```



---

## Category Ranking


Example:


```

Games

250GB


Applications

80GB


Documents

60GB


Cache

20GB


```



---

# File Tree Analysis


Purpose:


Analyze storage hierarchy.



---

# Tree Structure


```

Drive


|

├── Folder


│

├── SubFolder


│

└── File


```



---

# Tree Optimization


Avoid loading entire tree.



---

# Use:


Lazy Loading



---

# Example:


Only expand:


```

Users


```



when user opens it.



---

# File Search System


Purpose:


Quickly locate files.



---

# Search Conditions


Support:


```

File Name


↓

Extension


↓

Size Range


↓

Date Range


↓

Location


```



---

# Search Index


Optional:


Create lightweight index.



---

# Index Content


Store:


```

File Path


↓

Size


↓

Modified Time


↓

Category


```



---

# Index Update


Update:


When file changes.



---

# Real-time Monitoring


Optional.



Use:


System FileSystemWatcher.



---

# Hash Engine Optimization


Hash calculation is expensive.



---

# Optimization Strategy



## Small Files


Calculate:


Full SHA-256.



---

## Medium Files


Calculate:


Partial Hash first.



---

## Large Files


Use:


Multi-stage verification.



---

# Hash Pipeline


```

File Size


↓

Quick Hash


↓

Partial Hash


↓

Full Hash


```



---

# Parallel Hash Calculation


Allowed:



```

Multiple Worker Tasks


```



---

# Thread Control


Use:


```

SemaphoreSlim


```



---

# Memory Control


Never load:


Entire file into memory.



---

# Correct:


Stream reading.



---

# Example:


Wrong:


```

Read 50GB File


into Memory


```



Correct:


```

Read Blocks


↓

Calculate Hash


```



---

# Large File Scan Optimization


For large disks:



Use:


```

Directory Filtering


↓

Extension Filtering


↓

Size Filtering


```



---

# Ignore Paths


Default ignore:


```

System


↓

Program Files


↓

System Volume Information


```



---

# Exception Handling


Handle:


```

Access Denied


↓

File Locked


↓

Invalid Path


↓

Broken Link


```



---

# Cleanup Recommendation Engine


## Purpose


Recommend possible storage optimization.



---

# Recommendation Factors


Include:


```

File Age


↓

File Type


↓

Size


↓

Location


↓

Usage Frequency


```



---

# Recommendation Levels



## Safe


Example:


```

Temporary Files


Old Logs


```



---

## Review


Example:


```

Large Archive


Old Backup


```



---

## Protected


Example:


```

System Files


Application Files


```



---

# Automatic Cleanup Rules


File analyzer does not delete directly.



---

# Correct Flow:


```

Analyze


↓

Recommend


↓

User Confirm


↓

Cleanup Module


```



---

# Integration With Cleaner Engine


File Analyzer provides:


```

Candidate Files


↓

Risk Information


↓

Size Information


```



---

# Integration With Dashboard


Provide:


```

Storage Health


↓

Largest Files


↓

Recoverable Space


```



---

# Database Integration


Store:



## FileAnalysisRecords


Structure:


```

Id


Path


Size


Category


AnalysisTime


```



---

# Analysis Cache


Purpose:


Avoid repeated scans.



---

# Cache Data


Store:


```

File Path


↓

Size


↓

Hash


↓

Category


↓

Last Scan Time


```



---

# Cache Expiration


Recommended:


30 days.



---

# Performance Targets



## Normal Disk


Scan:


<30 seconds



---

## Large Disk


Scan:


<5 minutes



---

# Memory Target


Additional usage:


<200MB



---

# Security Restrictions


File Analyzer must never:


Delete automatically


↓

Modify system files


↓

Access protected personal data


↓

Upload file information



---

# Testing Requirements



## Small Disk Test


Verify:


Basic scanning.



---

## Large Disk Test


Millions of files.



---

## Duplicate Test


Same file different paths.



---

## Permission Test


Protected folders.



---

## Performance Test


Memory and CPU usage.



---

# File Analyzer Final Requirement


The file analyzer must provide:


Accurate storage understanding


↓

Useful cleanup suggestions


↓

Fast scanning


↓

Safe operation


↓

Professional user experience



END OF DOCUMENT


---