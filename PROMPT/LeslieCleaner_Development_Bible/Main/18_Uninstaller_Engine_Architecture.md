# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Uninstaller Engine Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture of the LeslieCleaner software uninstall engine.


The uninstall engine is responsible for:


Installed software detection


↓

Safe uninstall execution


↓

Residual file analysis


↓

Registry residual cleanup


↓

Startup residual cleanup


↓

Service residual cleanup



---

# Core Design Philosophy


Software uninstall is not only deleting application folders.


A complete uninstall process includes:


Application removal


↓

Residual detection


↓

Residual analysis


↓

Safe cleanup



---

# Design Goals


The uninstall module must provide:


Accurate application detection


↓

Safe uninstall process


↓

Complete residual analysis


↓

Low false deletion rate


↓

Recoverable operation



---

# Architecture Overview


```

Uninstaller Engine


        |

        |

+----------------+

Application Scanner

+----------------+

        |

        |

+----------------+

Fingerprint Engine

+----------------+

        |

        |

+----------------+

Uninstall Manager

+----------------+

        |

        |

+----------------+

Residual Analyzer

+----------------+

        |

        |

+----------------+

Cleanup Executor

+----------------+

        |

        |

+----------------+

Backup Manager

+----------------+


```



---

# Module Structure


```

Uninstaller


|

├── Scanner


├── Fingerprint


├── OfficialUninstall


├── ResidualScanner


├── Analyzer


├── Executor


├── Backup


├── Report


```



---

# Application Scanner


## Purpose


Detect installed software on System.



---

# Detection Sources


The scanner should analyze:



## System Registry


Main locations:


```



---

## Program Directory


Analyze:


```

C:\Program Files


C:\Program Files (x86)


User Application Paths


```



---

## Start Menu


Detect:


Application shortcuts.



---

## Startup Entries


Detect:


Application startup registrations.



---

# Application Model


```csharp

public class InstalledApplication

{

    public long Id { get; set; }


    public string Name { get; set; }


    public string Publisher { get; set; }


    public string Version { get; set; }


    public string InstallPath { get; set; }


    public string UninstallCommand { get; set; }


}


```



---

# Application Scanner Interface


```csharp

public interface IApplicationScanner

{

    Task<IEnumerable<InstalledApplication>>

    ScanAsync(CancellationToken token);


}

```



---

# Software Fingerprint System


## Purpose


Identify all resources belonging to an application.



---

# Why Fingerprint Is Needed


Applications leave many traces:


```

Files


↓

Folders


↓

Registry Keys


↓

Services


↓

Startup Entries


↓

Scheduled Tasks


```



---

# Fingerprint Structure


```

ApplicationFingerprint


|

├── File Paths


├── Registry Paths


├── Service Names


├── Startup Entries


├── Scheduled Tasks


└── Configuration Locations


```



---

# Fingerprint Example


Application:


```

Google Chrome


```



Fingerprint:


Files:


```

Chrome.exe


Chrome\User Data


```



Registry:


```

Google\Chrome


```



Startup:


```

Google Update


```



---

# Fingerprint Database


Store:


```

Applications


        |

        |

ApplicationFingerprints


```



---

# ApplicationFingerprints Table


Structure:


```

Id


ApplicationId


Type


Value


CreatedTime


```



---

# Type Values


```

File


Folder


Registry


Service


Startup


Task


```



---

# Official Uninstall Manager


## Purpose


Use the application's official uninstall method first.



---

# Uninstall Priority


Order:



```

Official Uninstaller


↓

MSI Uninstall


↓

System Package Manager


↓

Forced Cleanup


```



---

# Official Uninstall Detection


Read:


```

UninstallString


QuietUninstallString


ModifyPath


```



---

# Uninstall Flow


```

Select Application


↓

Analyze Application


↓

Create Backup


↓

Launch Official Uninstaller


↓

Wait Completion


↓

Residual Scan


```



---

# Uninstall Safety Check


Before uninstall:


Verify:


```

Application Exists


↓

Uninstall Command Valid


↓

Permission Available


↓

Backup Ready


```



---

# Running Application Detection


Before uninstall:



Check:


```

Process Running


↓

Locked Files


↓

Background Services


```



---

# User Notification


Example:


```

Chrome is currently running.

Please close it before uninstalling.


```



---

# Uninstall Logging


Record:


```

Application Name


↓

Start Time


↓

Method


↓

Result


↓

Residual Size


```



---

# Uninstall Report


Display:


```

Removed Files:

320


Removed Registry Entries:

45


Remaining Items:

12


```
---

# Residual Scanner


## Purpose


Find remaining data after application uninstall.



---

# Residual Types


The residual scanner analyzes:



```

Files


↓

Folders


↓

Registry Entries


↓

Services


↓

Startup Items


↓

Scheduled Tasks


```



---

# Residual Detection Architecture


```

Application Fingerprint


        |

        |

Residual Scanner


        |

        |

Analyzer


        |

        |

Cleanup Recommendation


```



---

# File Residual Detection


## Detection Methods


Use:


Application Fingerprint


↓

Path Matching


↓

File Metadata Analysis



---

# File Matching Rules


A file is considered related when:


```

Path matches fingerprint


OR


File name matches application


OR


Publisher matches application


OR


Hash matches known file


```



---

# File Risk Analysis


Each residual file receives risk level.



---

# Safe Residual


Examples:


```

Cache


Temporary Files


Logs


```



---

# Low Risk Residual


Examples:


```

Application Configuration


User Cache


```



---

# Medium Risk Residual


Examples:


```

Shared Components


Plugin Files


```



---

# High Risk Residual


Examples:


```

Shared Libraries


System Integration Files


```



---

# File Residual Rules


Never delete automatically:


```

User Documents


↓

Personal Files


↓

Shared System Files


```



---

# Registry Residual Detection


## Purpose


Find registry entries left by removed applications.



---

# Detection Sources


Analyze:


```

Application Name


↓

Publisher


↓

Install Path


↓

Fingerprint


```



---

# Registry Matching Example


Removed application:


```

ExampleApp


```



Registry:


```

HKCU\Software\ExampleApp


```



Result:


Possible residual.



---

# Registry Risk Analysis


Before deletion:


Check:


```

Is application installed?


↓

Is key referenced?


↓

Is system component?


↓

Is shared?


```



---

# Service Residual Detection


## Purpose


Detect services left after uninstall.



---

# Service Information


Analyze:


```

Service Name


Display Name


Executable Path


Publisher


Status


```



---

# Service Safety Rules


Never remove:


System Services.



---

# Service Removal Conditions


Allow removal only if:


```

Application Removed


AND


Service Path Missing


AND


Fingerprint Match


```



---

# Startup Residual Detection


## Purpose


Remove startup entries left by removed software.



---

# Detection Sources


```

Registry Startup


↓

Startup Folder


↓

Task Scheduler


↓

Service Startup


```



---

# Startup Cleanup Example


Removed:


```

Old Software


```



Remaining:


```

Startup Entry


→

Missing exe


```



Result:


Residual startup item.



---

# Scheduled Task Detection


Analyze:


```

Task Name


Action Path


Publisher


Creation Time


```



---

# Scheduled Task Safety


Only remove when:


```

Task belongs to removed software


AND


Executable missing


```



---

# Residual Analysis Engine


Purpose:


Determine cleanup recommendation.



---

# Analysis Factors


Include:


```

Application Relationship


↓

File Ownership


↓

Publisher


↓

Digital Signature


↓

Usage History


```



---

# Residual Confidence Score


Example:


```

0-30

Unknown


31-70

Possible


71-100

Confirmed


```



---

# Cleanup Recommendation


Based on score:



```

High Confidence


↓

Recommend Cleanup



Low Confidence


↓

Keep


```



---

# Forced Uninstall Mode


## Purpose


Remove applications without working uninstallers.



---

# When Used


Examples:


```

Broken Installer


↓

Missing Uninstaller


↓

Corrupted Software


```



---

# Forced Uninstall Process


```

Create Backup


↓

Terminate Processes


↓

Remove Files


↓

Remove Registry


↓

Remove Startup


↓

Generate Report


```



---

# Forced Uninstall Warning


Must display:


```

Forced uninstall may leave incomplete removal.

Continue?


```



---

# Process Termination


Before removing files:


Detect:


```

Running Process


↓

Locked Files


```



---

# Termination Rules


Allow:


Application process.



Do not terminate:


System process.



---

# Backup Integration


Before forced uninstall:


Create:



```

Registry Backup


↓

Configuration Backup


↓

Removal Record


```



---

# Restore Capability


If uninstall fails:


Allow:


```

Restore Registry


↓

Restore Files


↓

Recover Configuration


```



---

# Performance Optimization


## Application Scan


Use:


Cached application information.



---

# Residual Scan


Use:


Fingerprint filtering.



---

# Avoid:


Full disk scanning.



---

# Large Disk Optimization


For large drives:


Use:


```

Targeted Paths


↓

Metadata Search


↓

Incremental Index


```



---

# Multi-thread Support


Allowed:


```

Independent residual scanning tasks


```



---

# Thread Limit


Avoid excessive disk competition.



Recommended:


```

CPU Core Count


```



---

# Exception Handling


Handle:


```

Access Denied


↓

File Locked


↓

Registry Permission Error


↓

Missing Metadata


```



---

# Security Restrictions


Uninstaller must never:


Delete user documents


↓

Remove System components


↓

Modify security software


↓

Delete unknown shared files



---

# Testing Requirements



## Normal Uninstall


Test:


Popular applications.



---

## Broken Uninstall


Test:


Missing uninstall command.



---

## Residual Detection


Test:


Files


Registry


Startup


Services



---

## Safety Testing


Test:


System files


User files


Shared components



---

# Uninstaller Final Requirement


The uninstall engine must provide:


Complete removal


↓

Safe operation


↓

Transparent analysis


↓

Recoverable changes


↓

Professional user experience



END OF DOCUMENT


---