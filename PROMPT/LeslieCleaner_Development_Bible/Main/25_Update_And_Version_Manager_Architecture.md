# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Update And Version Manager Architecture Specification


Priority:

★★★★☆



---

# Purpose


This document defines the architecture and update management standards of the LeslieCleaner version management system.


The update manager is responsible for:


Version detection


↓

Update package management


↓

Secure installation


↓

Version migration


↓

Rollback recovery



---

# Core Design Philosophy


Software updates must be:


Safe


↓

Transparent


↓

Recoverable


↓

Verifiable



---

# Main Goals


The update system must provide:


Reliable version detection


↓

Secure update delivery


↓

Complete rollback capability


↓

Minimal user interruption


↓

Data protection



---

# Architecture Overview


```

Update Manager


        |

        |

+----------------+

Version Checker

+----------------+

        |

        |

+----------------+

Update Resolver

+----------------+

        |

        |

+----------------+

Package Manager

+----------------+

        |

        |

+----------------+

Installer Engine

+----------------+

        |

        |

+----------------+

Rollback Manager

+----------------+


```



---

# Module Structure


```

Update


|

├── Version


├── Checker


├── Resolver


├── Downloader


├── Validator


├── Installer


├── Migration


├── Rollback


└── Report


```



---

# Version Management System


## Purpose


Manage LeslieCleaner software versions.



---

# Version Format


Use:


Semantic Versioning



Format:


```

Major.Minor.Patch


```



Example:


```

1.5.3


```



---

# Version Meaning



## Major


Large architecture changes.



Example:


```

1.x.x

↓

2.x.x


```



---

## Minor


New features.



Example:


```

1.4.x

↓

1.5.x


```



---

## Patch


Bug fixes.



Example:


```

1.5.1

↓

1.5.2


```



---

# Version Model


```csharp

public class ApplicationVersion

{

    public string Version { get; set; }


    public string BuildNumber { get; set; }


    public DateTime ReleaseDate { get; set; }


    public string Channel { get; set; }


}


```



---

# Update Channels


Support:



## Stable


Recommended version.



---

## Beta


Testing version.



---

## Development


Internal testing.



---

# Default Channel


Normal users:


```

Stable


```



---

# Version Checker


## Purpose


Detect available updates.



---

# Checker Interface


```csharp

public interface IVersionChecker

{

    Task<ApplicationVersion>

    CheckAsync();


}

```



---

# Version Check Flow


```

Current Version


↓

Connect Update Source


↓

Compare Version


↓

Return Result


```



---

# Update Source


Support:



## Local Source


Example:


```

Offline Update Package


```



---

## Remote Source


Example:


```

Update Server


```



---

# Update Metadata


Server provides:


```

Latest Version


↓

Release Notes


↓

Package Information


↓

Hash


↓

Signature


```



---

# Update Metadata Example


```json

{

"version":"1.6.0",

"package":"LeslieCleaner.zip",

"sha256":"xxxx",

"mandatory":false

}


```



---

# Update Detection Rules


Compare:



```

Current Version


VS


Latest Version


```



---

# Update Result


Possible states:



```

No Update


↓

Optional Update


↓

Mandatory Update


↓

Unsupported Version


```



---

# Mandatory Update


Used for:


```

Critical Security Fix


↓

Database Migration Requirement


↓

Major Compatibility Issue


```



---

# Update Resolver


## Purpose


Determine update strategy.



---

# Update Types



## Full Update


Contains:


```

Complete Application Files


```



---

## Incremental Update


Contains:


```

Changed Files Only


```



---

# Full Update Usage


Suitable:


```

Major Version Change


↓

First Installation


↓

Corrupted Installation


```



---

# Incremental Update Usage


Suitable:


```

Small Version Upgrade


↓

Bug Fix


↓

Performance Patch


```



---

# Update Decision Logic


Example:


```

Version Difference


↓

Package Size


↓

Network Condition


↓

Select Update Method


```



---

# Update Package Structure


```

UpdatePackage


|

├── Files


├── Manifest


├── Migration


├── Signature


└── Changelog


```



---

# Update Manifest


Contains:


```

Target Version


↓

Required Version


↓

File List


↓

Hash List


↓

Migration Script


```



---

# File List Example


```json

{

"files":[

{

"path":"LeslieCleaner.exe",

"hash":"xxxx"

}

]

}


```



---

# Package Manager


## Responsibility


Manage update files.



---

# Package Manager Functions


```

Download


↓

Extract


↓

Verify


↓

Prepare


```



---

# Temporary Update Directory


Recommended:


```

LeslieCleaner


|

└── UpdateTemp


```



---

# Download Safety


During download:


Verify:


```

File Size


↓

Hash


↓

Network Complete


```



---

# Interrupted Download


Support:


```

Resume Download


```



---

# Download Cache


Store:


```

Downloaded Packages


```



---

# Cache Cleanup


Remove:


```

Expired Packages


↓

Failed Packages


```
---

# Update Validation System


## Purpose


Ensure update packages are complete and trustworthy before installation.



---

# Validation Pipeline


```

Receive Package


↓

Check File Integrity


↓

Verify Manifest


↓

Verify Signature


↓

Check Compatibility


↓

Allow Installation


```



---

# Manifest Validation


Check:



```

Package Version


↓

Required Version


↓

File List


↓

Dependency Information


```



---

# File Integrity Verification


Each file contains:


```

Path


↓

Size


↓

Hash


```



---

# Hash Algorithm


Recommended:


```

SHA-256


```



---

# Verification Example


```

Downloaded File


        |

        ↓


Calculate SHA-256


        |

        ↓


Compare Manifest


        |

        ↓


Pass / Fail


```



---

# Digital Signature System


## Purpose


Prevent malicious update packages.



---

# Signature Contents


Sign:


```

Package Manifest


↓

File Hash List


↓

Version Information


```



---

# Signature Verification Flow


```

Download Package


↓

Read Signature


↓

Verify Public Key


↓

Check Signature


↓

Trust Package


```



---

# Signature Failure Handling


If failed:



```

Block Installation


↓

Delete Temporary Package


↓

Generate Security Log


```



---

# Compatibility Check


Before update:



Check:


```

Current Version


↓

Operating System


↓

Architecture


↓

Dependencies


```



---

# Unsupported Update Example


Current:


```


```



Package:


```


```



Result:


Reject update.



---

# Update Installer Engine


## Purpose


Install verified update packages.



---

# Installation Strategy


Use:


```

Staged Replacement


```



---

# Installation Flow


```

Create Backup


↓

Stop Application


↓

Replace Files


↓

Run Migration


↓

Start Application


↓

Verify


```



---

# Application Shutdown


Before update:


Save:


```

User Settings


↓

Runtime State


↓

Database Connection


```



---

# File Replacement Strategy


Never:


Direct overwrite important files.



---

# Correct:


```

Old File


↓

Backup


↓

Replace


↓

Verify


```



---

# Locked File Handling


If file is locked:


Options:


```

Retry


↓

Schedule Replacement


↓

Restart Required


```



---

# Restart Update Mode


For locked files:



```

System Startup Task


↓

Replace Files


↓

Remove Task


```



---

# Database Migration System


## Purpose


Maintain compatibility between versions.



---

# Migration Example


Version:


```

1.5.0


```



Database:


```

Old Structure


```



Update:


```

1.6.0


```



Migration:


```

Add New Column


```



---

# Migration Model


```csharp

public class MigrationStep

{

    public string FromVersion { get; set; }


    public string ToVersion { get; set; }


    public string Script { get; set; }


}

```



---

# Migration Rules


Must support:


```

Forward Migration


↓

Migration Verification


↓

Rollback Plan


```



---

# Migration Failure Handling


If failed:


```

Stop Update


↓

Restore Database Backup


↓

Rollback Version


```



---

# Automatic Update System


## Purpose


Provide convenient upgrades.



---

# Update Modes


Support:



## Manual


User checks update.



---

## Notify


System informs user.



---

## Automatic


System downloads and installs.



---

# Default Mode


Recommended:


```

Notify


```



---

# Automatic Update Restrictions


Never update during:


```

Running Cleanup


↓

Uninstall Operation


↓

Backup Operation


```



---

# Update Schedule


User configurable:



```

Startup Check


↓

Daily Check


↓

Weekly Check


```



---

# Bandwidth Control


Support:


```

Download Limit


↓

Pause


↓

Resume


```



---

# Update Notification


Display:


```

New Version


↓

Changes


↓

Size


↓

Restart Requirement


```



---

# Rollback Manager


## Purpose


Recover from failed updates.



---

# Rollback Principle


Every update must be reversible.



---

# Rollback Flow


```

Detect Failure


↓

Stop Application


↓

Restore Backup


↓

Restore Database


↓

Start Previous Version


```



---

# Rollback Backup Contents


Include:



```

Executable Files


↓

Libraries


↓

Configuration


↓

Database


```



---

# Rollback Trigger


Automatic:



```

Startup Failure


↓

Migration Failure


↓

File Verification Failure


```



---

# Manual Rollback


User can select:


```

Previous Version


```



---

# Update History


Store:



```

Version


↓

Update Time


↓

Result


↓

Rollback Status


```



---

# Update History Database


Table:


```

UpdateRecords


```



Structure:


```

Id


OldVersion


NewVersion


Status


CreatedTime


Error


```



---

# Update Report


After completion:


Generate:


```

Previous Version


↓

New Version


↓

Changed Files


↓

Migration Result


↓

Restart Requirement


```



---

# Logging System


Record:


```

Check Update


↓

Download


↓

Verification


↓

Installation


↓

Rollback


```



---

# Error Handling


Handle:


```

Network Failure


↓

Hash Error


↓

Permission Error


↓

Disk Space Error


↓

Migration Error


```



---

# Performance Requirements



## Version Check


<3 seconds



---

## Package Verification


Depends on package size.



---

## Installation


Minimal interruption.



---

# Security Requirements


Update system must never:


Install unsigned packages


↓

Execute unknown scripts


↓

Modify unrelated files


↓

Bypass user permission



---

# Testing Requirements



## Update Test


Test:


Old version → New version



---

## Rollback Test


Test:


Failed update recovery



---

## Migration Test


Test:


Database compatibility



---

## Security Test


Test:


Modified package detection



---

## Network Test


Test:


Interrupted downloads



---

# Update Manager Final Requirement


The update system must provide:


Secure upgrades


↓

Reliable migration


↓

Complete rollback


↓

Transparent process


↓

Long-term maintainability



END OF DOCUMENT


---