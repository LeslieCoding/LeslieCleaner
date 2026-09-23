# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Backup And Restore System Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the complete backup and restore architecture of LeslieCleaner.


The backup system provides protection for:


Registry operations


↓

Cleanup operations


↓

Uninstall operations


↓

Configuration changes


↓

System optimization changes



---

# Core Design Philosophy


Every risky operation must be reversible.



The system follows:


```

Analyze


↓

Backup


↓

Execute


↓

Verify


↓

Restore If Needed


```



---

# Main Goals


The backup system must provide:


Safety


↓

Reliability


↓

Fast recovery


↓

Low storage usage


↓

Clear management



---

# Architecture Overview


```

Backup System


        |

        |

+----------------+

Backup Manager

+----------------+

        |

        |

+----------------+

Backup Providers

+----------------+

        |

        |

+----------------+

Storage Engine

+----------------+

        |

        |

+----------------+

Restore Engine

+----------------+

        |

        |

+----------------+

Verification

+----------------+


```



---

# Module Structure


```

Backup


|

├── Manager


├── Providers


├── Storage


├── Compression


├── Verification


├── Restore


├── Cleanup


├── Report


```



---

# Backup Types


The system supports:



## Registry Backup


Purpose:


Protect registry modifications.



---

## File Backup


Purpose:


Protect important files before operations.



---

## Configuration Backup


Purpose:


Save application settings.



---

## Database Backup


Purpose:


Protect LeslieCleaner internal data.



---

# Backup Manager


## Responsibility


Coordinate all backup operations.



---

# Backup Manager Interface


```csharp

public interface IBackupManager

{

    Task<BackupResult>

    CreateBackupAsync(

        BackupRequest request);


    Task<RestoreResult>

    RestoreAsync(

        BackupInfo backup);


}

```



---

# Backup Request Model


```csharp

public class BackupRequest

{

    public string Type { get; set; }


    public IEnumerable<string> Items { get; set; }


    public string Description { get; set; }


}

```



---

# Backup Information Model


```

BackupInfo


Id


Name


Type


Size


Path


CreatedTime


Status


```



---

# Backup Storage Architecture


Recommended structure:


```

LeslieCleaner


|

└── Backup


    |

    ├── Registry


    |

    ├── Files


    |

    ├── Database


    |

    └── Config


```



---

# Backup Naming Convention


Format:


```

Backup_Type_Date_Time


```



Example:


```

Registry_20260726_120000


```



---

# Registry Backup Provider


## Purpose


Export registry changes.



---

# Registry Backup Process


```

Select Keys


↓

Export .reg File


↓

Calculate Hash


↓

Save Metadata


↓

Complete


```



---

# Registry Backup Data


Store:


```

Registry Path


↓

Original Value


↓

Backup File


↓

Creation Time


```



---

# File Backup Provider


## Purpose


Protect files before deletion.



---

# File Backup Strategy


Three levels:



---

# Level 1

# Metadata Backup


Store:


```

Path


↓

Size


↓

Hash


↓

Modified Time


```



---

# Level 2

# Move Backup


Move files into backup storage.



---

# Level 3

# Full Copy Backup


Create complete duplicate.



---

# Backup Level Selection


Based on:


```

Risk Level


↓

File Size


↓

Operation Type


```



---

# Example:


Temporary Cache:


```

Metadata Only


```



Registry Modification:


```

Full Backup


```



---

# Configuration Backup


Purpose:


Save LeslieCleaner settings.



---

# Configuration Data


Include:


```

User Preferences


↓

Scan Rules


↓

Whitelist


↓

Blacklist


↓

Custom Settings


```



---

# Configuration Format


Recommended:


```

JSON


```



Example:


```

settings.json


```



---

# Database Backup


Purpose:


Protect application database.



---

# Database Backup Content


Include:


```

LeslieCleaner.db


↓

Migration Information


↓

User Data


```



---

# SQLite Backup Method


Use:


SQLite Backup API



---

# Database Backup Frequency


Recommended:



Automatic:


Daily



Manual:


Before major operations



---

# Backup Metadata Database


Store:


```

BackupRecords


```



Structure:


```

Id


BackupType


BackupPath


Size


Hash


Status


CreatedTime


```



---

# Backup Verification


Every backup must be verified.



---

# Verification Methods


Check:


```

File Exists


↓

Size Match


↓

Hash Match


↓

Readable


```



---

# Hash Verification


Recommended:


SHA-256



---

# Backup Compression


Purpose:


Reduce storage usage.



---

# Compression Format


Recommended:


```

ZIP


```



---

# Compression Rules


Compress:


```

Large Files


↓

Database Backup


↓

Configuration Backup


```



---

# Do Not Compress:


Already compressed:


```

ZIP


RAR


MP4


ISO


```



---

# Backup Security


Backup files must:


Require local access


↓

Prevent unauthorized modification


↓

Maintain integrity



---

---

# Restore Engine


## Purpose


Restore system state after failed or unwanted operations.



---

# Restore Types



## Registry Restore


Restore:


```

Registry Keys


↓

Registry Values


```



---

## File Restore


Restore:


```

Deleted Files


↓

Original Location


```



---

## Configuration Restore


Restore:


```

Application Settings


↓

User Preferences


```



---

## Database Restore


Restore:


```

LeslieCleaner Database


```



---

# Restore Flow


```

User Selects Backup


↓

Verify Backup Integrity


↓

Create Current State Backup


↓

Execute Restore


↓

Validate Result


↓

Generate Report


```



---

# Restore Safety Principle


Never overwrite current data directly.



---

# Correct Process:


```

Current State Backup


↓

Restore Previous State


↓

Verify


```



---

# Restore Conflict Handling


If target exists:


Options:


```

Replace


↓

Keep Current


↓

Create Copy


```



---

# Restore Verification


After restore:


Check:


```

File Exists


↓

Hash Match


↓

Registry Valid


↓

Database Accessible


```



---

# Restore Failure Handling


If restore fails:


```

Stop Operation


↓

Rollback


↓

Generate Error Report


```



---

# Automatic Backup Strategy


## Purpose


Protect user without requiring manual operation.



---

# Automatic Backup Triggers



## Before Risky Operations


Examples:


```

Registry Repair


↓

Forced Uninstall


↓

Large Cleanup


```



---

## Before Software Upgrade


Backup:


```

Configuration


↓

Database


```



---

## Scheduled Backup


Optional.



---

# Scheduled Backup Options


User can configure:


```

Daily


↓

Weekly


↓

Monthly


```



---

# Default Automatic Backup


Recommended:


Enable only important backups.



---

# Avoid:


Creating huge backups every day.



---

# Backup Retention Management


## Purpose


Control storage usage.



---

# Retention Policy


Default:


```

Keep Latest 10 Backups


```



---

# User Configuration


Allow:


```

Number Based


↓

Time Based


↓

Storage Size Based


```



---

# Storage Size Rule


Example:


Maximum:


```

10GB


```



When exceeded:


Remove oldest backups.



---

# Backup Cleanup Process


```

Calculate Backup Size


↓

Sort By Time


↓

Delete Old Backups


↓

Verify Space


```



---

# Backup Priority


Never delete:


Recent backup


↓

Important operation backup


↓

User locked backup



---

# Backup Lock


User can protect backup.



---

# Locked Backup


Cannot be automatically deleted.



---

# Backup Compression Optimization


For large backups:



Use:


```

Streaming Compression


```



---

# Avoid:


Loading entire backup into memory.



---

# Backup Encryption


Optional feature.



---

# Encryption Usage


Suitable for:


```

Sensitive Configuration


↓

Private Backup Data


```



---

# Encryption Rules


Never store:


Plaintext sensitive backup data.



---

# Backup Integrity


Each backup stores:


```

Hash


↓

Size


↓

Creation Time


↓

Version


```



---

# Disaster Recovery System


## Purpose


Recover LeslieCleaner after serious failure.



---

# Disaster Scenarios


Support:


```

Database Corruption


↓

Configuration Damage


↓

Failed Upgrade


↓

Incomplete Cleanup


```



---

# Recovery Mode


Application startup checks:



```

Database Valid


↓

Configuration Valid


↓

Required Files Exist


```



---

# Recovery Detection


If failure:


Enter:


```

Recovery Mode


```



---

# Recovery Mode Functions


Provide:


```

Restore Backup


↓

Repair Installation


↓

Reset Configuration


↓

Export Logs


```



---

# Emergency Backup


Before recovery:


Create:


```

Emergency Snapshot


```



---

# Recovery Report


Contains:


```

Problem


↓

Recovery Action


↓

Result


↓

Remaining Issues


```



---

# Backup Database Tables


## BackupRecords


Structure:


```

Id


Type


Name


Path


Size


Hash


Status


CreatedTime


```



---

## RestoreRecords


Structure:


```

Id


BackupId


Operation


Result


Error


CreatedTime


```



---

# Performance Optimization


## Backup Streaming


Large files use:


```

Stream Copy


```



---

# Parallel Backup


Allowed for independent files.



---

# Thread Control


Avoid excessive disk usage.



---

# Recommended:


```

2-4 Workers


```



---

# Logging Requirements


Record:


```

Backup Created


↓

Backup Verified


↓

Restore Started


↓

Restore Completed


↓

Restore Failed


```



---

# Security Requirements


Backup system must never:


Upload backup data


↓

Access unrelated user files


↓

Delete protected backups


↓

Modify backups silently



---

# Testing Requirements



## Backup Test


Verify:


Creation


↓

Storage


↓

Verification



---

## Restore Test


Verify:


Complete recovery.



---

## Failure Test


Simulate:


Disk error


↓

Permission error


↓

Corrupted backup



---

## Retention Test


Verify:


Old backup cleanup.



---

# Backup System Final Requirement


The backup and restore system must provide:


Reliable protection


↓

Fast recovery


↓

Low storage overhead


↓

Complete transparency


↓

Long-term stability



END OF DOCUMENT