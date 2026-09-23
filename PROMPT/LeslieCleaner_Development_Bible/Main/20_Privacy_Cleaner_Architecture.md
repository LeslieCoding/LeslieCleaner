
# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Privacy Cleaner Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture and safety standards of the LeslieCleaner privacy cleaning module.


The privacy cleaner is responsible for:


Browser data analysis


↓

Privacy risk detection


↓

Temporary privacy data cleanup


↓

User-controlled removal


↓

Secure operation



---

# Core Design Philosophy


Privacy cleaning must respect user data.



The module must:

Protect personal information


↓

Never collect private content


↓

Never upload user data


↓

Always require user control



---

# Main Goals


The privacy module must provide:


Safe browser cleanup


↓

Transparent data handling


↓

Clear privacy explanation


↓

Reliable recovery



---

# Architecture Overview


```

Privacy Cleaner


        |

        |

+----------------+

Browser Scanner

+----------------+

        |

        |

+----------------+

Privacy Analyzer

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

Backup Manager

+----------------+


```



---

# Module Structure


```

Privacy


|

├── Browser


├── Scanner


├── Analyzer


├── Rules


├── Backup


├── Executor


├── Report


```



---

# Supported Browsers


Initial support:


```

Google Chrome


Microsoft Edge


Mozilla Firefox


Brave Browser


```



---

# Browser Detection System


Purpose:


Automatically find installed browsers.



---

# Detection Sources


Analyze:


```

Installation Directory


↓

Registry


↓

User Profile Directory


↓

Executable Location


```



---

# Browser Information Model


```csharp

public class BrowserInfo

{

    public long Id { get; set; }


    public string Name { get; set; }


    public string Version { get; set; }


    public string InstallPath { get; set; }


    public string ProfilePath { get; set; }


}


```



---

# Browser Provider Interface


```csharp

public interface IBrowserProvider

{

    Task<BrowserInfo>

    DetectAsync();


    Task<IEnumerable<PrivacyItem>>

    ScanAsync();


}

```



---

# Browser Profile Detection


Browsers may contain multiple profiles.



---

# Example:


Chrome:


```

Default


Profile 1


Profile 2


```



---

# Profile Model


Contains:


```

Profile Name


↓

Profile Path


↓

Last Used Time


```



---

# Privacy Item Model


```csharp

public class PrivacyItem

{

    public long Id { get; set; }


    public string Browser { get; set; }


    public string Type { get; set; }


    public string Path { get; set; }


    public long Size { get; set; }


    public int RiskLevel { get; set; }


}


```



---

# Privacy Data Categories


Main categories:



## Cache


Contains:


```

Images


↓

Scripts


↓

Temporary Resources


```



---

## History


Contains:


```

Visited URLs


↓

Browsing Records


```



---

## Cookies


Contains:


```

Website Session Data


↓

Login State


```



---

## Download History


Contains:


```

Downloaded File Records


```



---

## Session Data


Contains:


```

Open Tabs


↓

Recovery Data


```



---

# Privacy Risk Classification


Each item receives risk level.



---

# Low Risk


Example:


```

Browser Cache


Temporary Resources


```



---

# Medium Risk


Example:


```

Download History


Website History


```



---

# High Risk


Example:


```

Cookies


Saved Sessions


Authentication Data


```



---

# Critical Protection


Never clean automatically:


```

Saved Passwords


↓

Payment Information


↓

Security Certificates


```



---

# Cookie Management Strategy


Cookies require special handling.



---

# Default Behavior


Do not delete all cookies automatically.



---

# Reason:


Cookies may contain:


```

Login State


↓

User Preferences


↓

Authentication Information


```



---

# Cookie Options


Provide:


```

Keep All Cookies


↓

Remove Tracking Cookies


↓

Remove Selected Cookies


```



---

# Tracking Cookie Detection


Analyze:


```

Known Tracking Domains


↓

Third-party Cookies


↓

Expiration Information


```



---

# Browser Running Detection


Before cleanup:


Check:


```

Browser Process


↓

Locked Database


↓

Active Session


```



---

# Browser Close Handling


If browser running:


Show:


```

Chrome is running.

Close browser before cleaning.


```



---

# Privacy Scanner Flow


```

Detect Browser


↓

Find Profiles


↓

Analyze Data


↓

Calculate Risk


↓

Show Result


↓

User Selects


↓

Cleanup


```
---

# Browser Database Handling


## Purpose


Safely process browser internal databases.



---

# Browser Data Storage Types


Common formats:


```

SQLite Database


↓

JSON Configuration


↓

Binary Cache Files


```



---

# Database Examples


Chrome / Edge:


```

History


Cookies


Web Data


Login Data


```



---

# Firefox:


```

places.sqlite


cookies.sqlite


```



---

# Safety Requirement


Never directly modify browser databases while browser is running.



---

# Database Processing Flow


```

Detect Browser


↓

Check Process


↓

Create Temporary Backup


↓

Copy Database


↓

Analyze Copy


↓

Execute Cleanup


↓

Replace Original


```



---

# Temporary Database Copy


Reason:


Avoid corruption.



---

# Example:


Original:


```

Cookies


```



Processing:


```

Cookies Copy


↓

Analyze


↓

Modify


↓

Replace


```



---

# Browser Cache Cleanup


## Purpose


Remove unnecessary cached resources.



---

# Cache Detection


Analyze:


```

Cache Directory


↓

File Age


↓

File Size


↓

Last Access Time


```



---

# Cache Safety Rules


Allow cleanup:


```

Expired Cache


↓

Temporary Cache


```



---

# Keep:


```

Active Session Data


↓

Important Browser Configuration


```



---

# History Cleanup


## Purpose


Remove browsing history records.



---

# History Options


Provide:


```

Last Hour


↓

Today


↓

Last Week


↓

All Time


↓

Custom Range


```



---

# History Risk


Medium.



Reason:


Contains user browsing information.



---

# Download History Cleanup


## Important


Only remove records.



Do not delete downloaded files.



---

# Example:


Remove:


```

Download Record


```



Keep:


```

D:\Downloads\File.zip


```



---

# Session Cleanup


Session data includes:


```

Recently Closed Tabs


↓

Restore Information


```



---

# Default:


Protected.



---

# User Choice:


Allow manual cleanup.



---

# Cookie Selection Algorithm


## Purpose


Remove unwanted cookies safely.



---

# Cookie Classification


Categories:


```

Essential Cookie


↓

User Preference Cookie


↓

Tracking Cookie


↓

Advertising Cookie


```



---

# Essential Cookie


Examples:


```

Login Session


Security Token


```



Default:


Protected.



---

# Preference Cookie


Examples:


```

Language Setting


Theme Setting


```



Optional cleanup.



---

# Tracking Cookie


Examples:


```

Analytics


Advertising Tracking


```



Recommend cleanup.



---

# Cookie Risk Score


Formula:


```

Risk =


Tracking Level


+

Third Party Level


+

Expiration Status


```



---

# Privacy Risk Engine


## Purpose


Calculate privacy exposure.



---

# Risk Factors


Include:


```

Stored History Amount


↓

Cookie Count


↓

Tracking Domains


↓

Session Data


↓

Privacy Configuration


```



---

# Privacy Score


Range:


```

0-100


```



---

# Example:


Low Risk:


```

90


```



High Risk:


```

30


```



---

# Privacy Recommendation Engine


Generate suggestions.



---

# Example:


Problem:


```

3000 tracking cookies detected


```



Suggestion:


```

Remove tracking cookies


```



---

# Secure Cleanup Executor


Responsible:


Execute privacy cleanup.



---

# Cleanup Rules


Before removal:


Check:


```

User Selection


↓

Risk Level


↓

Backup Requirement


↓

Browser State


```



---

# Backup Strategy


For important privacy data:



Create:


```

BrowserDataBackup


```



---

# Backup Contents


Can include:


```

Cookies


↓

History


↓

Configuration


```



---

# Backup Restrictions


Never backup:


```

Passwords


↓

Payment Data


↓

Authentication Secrets


```



---

# Privacy Report


After cleanup:


Generate:


```

Browser Name


↓

Cleaned Items


↓

Released Space


↓

Privacy Improvement


```



---

# Example:


```

Chrome


Removed:


5200 Tracking Cookies


Freed:


850MB


```



---

# Multi-browser Parallel Processing


Supported.



---

# Execution Model


```

Chrome Scanner


        |


Edge Scanner


        |


Firefox Scanner


        |


        ↓


Result Merge


```



---

# Performance Optimization


## Cache Browser Path


Avoid repeated detection.



---

# Incremental Scan


Only analyze changed data.



---

# Large Cache Optimization


Use:


```

File Metadata


↓

Directory Statistics


↓

Batch Processing


```



---

# Exception Handling


Handle:


```

Browser Locked


↓

Database Corruption


↓

Permission Denied


↓

Missing Profile


```



---

# Logging


Record:


```

Browser Detection


↓

Scan Result


↓

Cleanup Action


↓

Failure


```



---

# Security Restrictions


Privacy cleaner must never:


Read passwords


↓

Upload browsing data


↓

Collect personal information


↓

Modify security settings



---

# Testing Requirements



## Browser Test


Test:


Chrome


↓

Edge


↓

Firefox


↓

Brave



---

## Data Safety Test


Verify:


History removal


↓

Cookie selection


↓

Cache cleanup



---

## Running Browser Test


Verify:


Locked database handling.



---

## Recovery Test


Verify:


Backup restore.



---

# Privacy Cleaner Final Requirement


The privacy module must provide:


Strong privacy protection


↓

Safe cleanup


↓

User control


↓

No personal data leakage


↓

Reliable recovery



END OF DOCUMENT


---