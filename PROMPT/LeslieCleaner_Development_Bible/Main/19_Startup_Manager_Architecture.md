# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Startup Manager Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner System startup management module.


The startup manager is responsible for:


Startup item detection


↓

Startup classification


↓

Boot impact analysis


↓

Safe optimization


↓

Startup restoration



---

# Core Design Philosophy


Startup optimization is not simply disabling everything.


The system must distinguish:


```

System Required Components


↓

User Installed Applications


```



---

# Main Goals


The startup manager must provide:


Accurate detection


↓

Clear classification


↓

Safe control


↓

Performance improvement


↓

Complete recovery



---

# Architecture Overview


```

Startup Manager


        |

        |

+----------------+

Startup Scanner

+----------------+

        |

        |

+----------------+

Classifier

+----------------+

        |

        |

+----------------+

Impact Analyzer

+----------------+

        |

        |

+----------------+

Action Controller

+----------------+

        |

        |

+----------------+

Restore Manager

+----------------+



```



---

# Module Structure


```

Startup


|

├── Scanner


├── Providers


├── Classifier


├── Analyzer


├── Controller


├── Backup


├── Restore


├── Report


```



---

# Startup Scanner


## Purpose


Collect all System startup entries.



---

# Startup Sources


The scanner must analyze:



## Registry Startup


Locations:



### Current User


```


```



---

### Local Machine


```


```



---

## Startup Folder


Locations:


User startup folder


↓

Common startup folder



---

## Task Scheduler


Analyze:


```

Scheduled Tasks


Task Actions


Triggers


```



---

## System Services


Analyze:


```

Service Startup Type


Executable Path


Publisher


```



---

# Startup Item Model


```csharp

public class StartupItem

{

    public long Id { get; set; }


    public string Name { get; set; }


    public string Publisher { get; set; }


    public string Path { get; set; }


    public string Command { get; set; }


    public StartupSource Source { get; set; }


    public StartupCategory Category { get; set; }


    public bool Enabled { get; set; }


}


```



---

# Startup Source Enumeration


```

Registry


StartupFolder


TaskScheduler


Service


```



---

# Scanner Interface


```csharp

public interface IStartupScanner

{

    Task<IEnumerable<StartupItem>>

    ScanAsync(CancellationToken token);


}

```



---

# Startup Classification System


## Purpose


Determine whether startup item is safe to optimize.



---

# Classification Categories


Required:



```

MicrosoftSystem


↓

UserApplication


↓

Unknown


```



---

# Microsoft System Category


Definition:


System built-in components.



Examples:


```

System Security


System Update


System Services


Hardware Drivers


```



---

# User Application Category


Definition:


Third-party software installed by user.



Examples:


```

Steam


Discord


Adobe


Cloud Sync Software


```



---

# Unknown Category


Definition:


Cannot confidently identify.



Examples:


```

Unknown Publisher


Missing File


Unverified Program


```



---

# Classification Rules


Analyze:


```

Publisher


↓

Digital Signature


↓

File Location


↓

Install Information


↓

Database Fingerprint


```



---

# Microsoft Detection Rules


Mark as Microsoft System if:


```

Publisher = Microsoft


OR


Path belongs to System folder


OR


Digital Signature verified by Microsoft


```



---

# Important Safety Rule


Publisher alone is not enough.



Must combine:



```

Publisher


+

Path


+

Signature


```



---

# User Application Detection


Mark as User Application if:


```

Installed under Program Files


OR


Registered in uninstall database


OR


Known software fingerprint


```



---

# Unknown Handling


Unknown items:


Default:


Do not recommend disabling automatically.



---

# Startup Provider Architecture


Each startup source should implement:



```csharp

public interface IStartupProvider

{

    Task<IEnumerable<StartupItem>>

    GetItemsAsync();


    Task EnableAsync(StartupItem item);


    Task DisableAsync(StartupItem item);


}

```



---

# Registry Startup Provider


Responsible:


Read and modify registry startup entries.



---

# Registry Provider Safety


Before modification:


Create backup.



---

# Startup Folder Provider


Responsible:


Manage shortcuts.



---

# Shortcut Analysis


Check:


```

Target Path


↓

Existence


↓

Publisher


```



---

# Task Scheduler Provider


Analyze:


```

Task Name


Action


Trigger


Status


```



---

# Service Provider


Analyze:


```

Service Name


Display Name


Startup Type


Executable


```



---

# Service Restrictions


Never disable:


System critical services.



---

# Startup Manager Database


Uses:


StartupItems table.



Stores:


```

Current Status


↓

Original Status


↓

Backup Information


↓

User Actions


```
---

# Startup Impact Analyzer


## Purpose


Evaluate how much each startup item affects System boot performance.



---

# Impact Factors


The analyzer calculates impact based on:



```

Startup Delay


↓

CPU Usage


↓

Memory Usage


↓

Disk Usage


↓

Application Importance


↓

Startup Frequency


```



---

# Startup Impact Score


Range:


```

0 - 100


```



---

# Score Meaning



## 0-30


Low Impact



Example:


Small helper applications.



---

## 31-70


Medium Impact



Example:


Cloud synchronization tools.



---

## 71-100


High Impact



Example:


Heavy background applications.



---

# Calculation Example


Formula:


```

Impact Score =


CPU Weight

+

Memory Weight

+

Disk Weight

+

Startup Time Weight


```



---

# Startup Time Measurement


Measure:


```

Application Launch Time


↓

First Response Time


↓

Background Initialization


```



---

# Performance Data Collection


Sources:


System Performance Counters


↓

Application History


↓

User Operation Data



---

# Startup Recommendation Engine


Purpose:


Generate optimization suggestions.



---

# Recommendation Rules



## High Impact + User Application


Recommend:


```

Disable


```



---

## Medium Impact + Frequently Used


Recommend:


```

Keep


or


Delay Start


```



---

## Microsoft System


Recommend:


```

Protected


```



---

# Disable Operation Design


## Purpose


Safely disable startup items.



---

# Disable Flow


```

User Selects Item


↓

Check Category


↓

Check Protection


↓

Create Backup


↓

Disable Startup


↓

Record Operation


```



---

# Disable Restrictions



Cannot disable automatically:



```

MicrosoftSystem


+

Protected


```



---

# Warning Message Example


```

This startup item belongs to System system components.

Disabling it may affect system stability.


```



---

# Backup System


Before disabling:


Save:


```

Original Location


↓

Original Value


↓

Original Status


```



---

# Registry Startup Backup Example


Store:


```

Registry Path:

HKCU\...\Run


Value Name:

ApplicationName


Value Data:

xxx.exe


```



---

# Restore System


Purpose:


Recover disabled startup items.



---

# Restore Flow


```

Select Item


↓

Read Backup


↓

Restore Original State


↓

Verify


↓

Update Database


```



---

# Delayed Startup System


## Purpose


Improve boot speed without disabling software.



---

# Delay Strategy


Instead of:


Start immediately.



Use:


```

System Login


↓

Wait Delay Time


↓

Start Application


```



---

# Delay Options


Example:


```

30 Seconds


60 Seconds


120 Seconds


300 Seconds


```



---

# Delay Implementation


Recommended:


System Task Scheduler.



---

# Delay Task Structure


```

Trigger:

User Login


↓

Delay:

60 seconds


↓

Action:

Launch Application


```



---

# Delay Restrictions


Do not delay:


```

Security Software


↓

Hardware Drivers


↓

System Components


```



---

# Startup Optimization Report


After optimization:


Generate:



```

Before Score


↓

After Score


↓

Disabled Items


↓

Delayed Items


↓

Estimated Improvement


```



---

# Example:


```

Startup Time:


45s


↓

28s



Improvement:


37%


```



---

# Startup History


Record:


```

Boot Time


↓

Startup Changes


↓

Performance Trend


```



---

# Boot Performance Tracking


Optional feature.



Monitor:


```

System Boot Duration


↓

Application Startup Time


```



---

# Startup Cache


Purpose:


Reduce repeated scanning.



---

# Cache Data


Store:


```

Startup Item


↓

Last Analysis


↓

Impact Score


↓

Publisher


```



---

# Cache Expiration


Recommended:


7 days.



---

# Multi-thread Scanning


Startup sources can scan independently.



---

# Example:


```

Registry Scanner


        |


Task Scheduler Scanner


        |


Service Scanner


        |


        ↓


Result Merge


```



---

# Exception Handling


Handle:


```

Access Denied


↓

Missing File


↓

Invalid Registry


↓

Service Failure


```



---

# Logging


Record:



```

Startup Scan


↓

Enable Operation


↓

Disable Operation


↓

Restore Operation


```



---

# Security Rules


Startup Manager must never:


Modify bootloader


↓

Disable System Security


↓

Remove system services


↓

Change critical drivers



---

# Testing Requirements



## Registry Startup Test


Test:


Enable


↓

Disable


↓

Restore



---

## Startup Folder Test


Test:


Shortcut creation


↓

Removal



---

## Task Scheduler Test


Test:


Create


↓

Disable


↓

Restore



---

## Service Test


Test:


Protected service handling.



---

# Performance Target


Startup scan:


<5 seconds



---

# Memory Target


Additional memory:


<50MB



---

# Startup Manager Final Requirement


The startup manager must achieve:


Faster boot time


↓

Clear recommendations


↓

Safe optimization


↓

Complete recovery


↓

No system damage



END OF DOCUMENT


---