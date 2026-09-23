# LeslieCleaner Development Bible

Version:

1.0


Document Type:

Plugin System Architecture Specification


Priority:

★★★★☆



---

# Purpose


This document defines the architecture and extension standards of the LeslieCleaner plugin system.


The plugin system provides:


Feature expansion


↓

Module isolation


↓

Flexible development


↓

Future scalability



---

# Core Design Philosophy


The plugin system allows extending LeslieCleaner without modifying the core program.



Architecture:


```

Core System


↓

Plugin Interface


↓

Plugin Modules


```



---

# Main Goals


The plugin system must provide:


Easy extension


↓

Stable loading


↓

Permission control


↓

Security isolation


↓

Version compatibility



---

# Plugin Architecture Overview


```

LeslieCleaner Core


        |

        |

+----------------+

Plugin Manager

+----------------+

        |

        |

+----------------+

Plugin Loader

+----------------+

        |

        |

+----------------+

Plugin Runtime

+----------------+

        |

        |

+----------------+

Plugin Modules

+----------------+


```



---

# Module Structure


```

Plugin


|

├── Manager


├── Loader


├── Interface


├── Runtime


├── Security


├── Config


├── Update


```



---

# Plugin Types


Supported:



## Cleaner Plugin


Purpose:


Add new cleaning rules.



Example:


```

Third-party cache cleaner


```



---

## Analyzer Plugin


Purpose:


Add analysis functions.



Example:


```

Hardware analyzer


```



---

## AI Plugin


Purpose:


Extend AI capabilities.



Example:


```

Custom AI provider


```



---

## UI Plugin


Purpose:


Add interface components.



Example:


```

Dashboard widget


```



---

# Plugin Package Structure


Recommended:


```

PluginName


|

├── Plugin.dll


├── manifest.json


├── Resources


├── Config


└── Documentation


```



---

# Plugin Manifest


Every plugin requires:


```

manifest.json


```



---

# Manifest Example


```json

{

"name":"ExampleCleaner",

"version":"1.0.0",

"author":"Developer",

"type":"Cleaner",

"apiVersion":"1.0"

}


```



---

# Manifest Fields



## Name


Plugin identifier.



---

## Version


Plugin version.



---

## Author


Plugin developer information.



---

## Type


Plugin category.



---

## API Version


Compatible LeslieCleaner plugin API version.



---

# Plugin Directory


Recommended:


```

LeslieCleaner


|

└── Plugins


    |

    ├── PluginA


    |

    └── PluginB


```



---

# Plugin Manager


## Responsibility


Manage all plugins.



---

# Plugin Manager Functions


Include:


```

Install


↓

Load


↓

Enable


↓

Disable


↓

Remove


↓

Update


```



---

# Plugin Manager Interface


```csharp

public interface IPluginManager

{

    Task LoadPluginsAsync();


    Task InstallAsync(string path);


    Task EnableAsync(string id);


    Task DisableAsync(string id);


    Task RemoveAsync(string id);


}

```



---

# Plugin Loader


## Purpose


Load plugin assemblies dynamically.



---

# Loading Process


```

Find Plugin Folder


↓

Read Manifest


↓

Validate Version


↓

Load Assembly


↓

Create Instance


↓

Register Services


```



---

# Dynamic Loading


Recommended:


.NET AssemblyLoadContext



---

# Loading Isolation


Each plugin should have:



```

Independent Context


↓

Independent Dependencies


```



---

# Plugin Interface


All plugins implement:



```csharp

public interface IPlugin

{

    string Id { get; }


    string Name { get; }


    string Version { get; }


    void Initialize();


    void Shutdown();

}

```



---

# Plugin Lifecycle


Stages:



## Installed


Plugin files exist.



---

## Loaded


Plugin assembly loaded.



---

## Enabled


Plugin functions available.



---

## Disabled


Plugin remains installed but inactive.



---

## Removed


Plugin completely deleted.



---

# Lifecycle Flow


```

Install


↓

Load


↓

Initialize


↓

Enable


↓

Run


↓

Shutdown


↓

Remove


```



---

# Plugin Initialization


When starting:


```

Validate Plugin


↓

Load Dependencies


↓

Register Services


↓

Initialize


```



---

# Plugin Shutdown


Before closing:


```

Stop Tasks


↓

Release Resources


↓

Save State


```



---

# Plugin State Management


Store:



```

PluginId


↓

Enabled Status


↓

Version


↓

Install Time


↓

Configuration


```



---

# Database Integration


Create table:


```

Plugins


```



Structure:


```

Id


PluginId


Name


Version


Status


Path


CreatedTime


UpdatedTime


```



---

# Plugin Configuration


Each plugin can have:



```

plugin.json


```



or:



```

SQLite Configuration


```



---

# Configuration Example


```json

{

"scanDepth":3,

"enabled":true

}


```



---

# Plugin Dependency System


Plugins may require:


```

Core API Version


↓

Other Plugins


↓

Libraries


```



---

# Dependency Check


Before loading:


Verify:


```

Required Version


↓

Required Plugin


↓

Required Library


```



---

# Version Compatibility


Use:


Semantic Versioning



Example:


```

1.2.0


```



---

# Compatibility Rule


Plugin API:


```

Major Version Match


```



---

# Plugin System Final Principle


Plugins should expand LeslieCleaner.


They must not:


Damage core stability


↓

Break security


↓

Reduce performance



---

---

# Plugin Permission System


## Purpose


Control what plugins are allowed to access.



---

# Core Principle


Plugins follow:


```

Minimum Permission Principle


```



---

# Permission Categories


## File Permission


Controls:


```

Read Files


↓

Write Files


↓

Delete Files


```



---

## Registry Permission


Controls:


```

Read Registry


↓

Modify Registry


↓

Delete Registry


```



---

## Network Permission


Controls:


```

Internet Access


↓

Remote API


↓

Data Upload


```



---

## System Permission


Controls:


```

Service Access


↓

Process Control


↓

System Configuration


```



---

# Permission Manifest


Plugins declare required permissions.



Example:


```json

{

"permissions":[

"File.Read",

"Registry.Read"

]

}


```



---

# Permission Approval Flow


```

Plugin Installation


↓

Read Permissions


↓

Show User


↓

User Approves


↓

Enable Plugin


```



---

# Default Permission


New plugins:


```

No Dangerous Permission


```



---

# Dangerous Permissions


Require extra confirmation:



```

Registry.Write


↓

File.Delete


↓

Process.Terminate


↓

Service.Modify


```



---

# Plugin Sandbox System


## Purpose


Prevent plugin failures affecting core system.



---

# Isolation Strategy


Use:


```

Separate Execution Context


↓

Permission Boundary


↓

Resource Monitoring


```



---

# Plugin Failure Isolation


If plugin crashes:



```

Catch Exception


↓

Disable Plugin


↓

Record Error


↓

Keep Core Running


```



---

# Plugin Runtime Manager


Responsible for:


```

Start Plugin


↓

Monitor Plugin


↓

Stop Plugin


↓

Recover Plugin


```



---

# Plugin Resource Limitation


## CPU Limit


Prevent:


```

Infinite Loop


↓

High CPU Usage


```



---

# Memory Limit


Monitor:


```

Plugin Memory Usage


```



---

# Thread Limit


Plugins cannot create unlimited threads.



---

# Execution Timeout


Long operations:


Require:


```

CancellationToken


```



---

# Plugin Security Scanner


## Purpose


Check plugin safety before installation.



---

# Scan Items


Analyze:



## File Structure


Check:


```

Missing Manifest


↓

Invalid Files


```



---

## Assembly Information


Check:


```

Publisher


↓

Version


↓

Dependencies


```



---

## Permission Analysis


Check:


```

Requested Permissions


```



---

## Behavior Analysis


Detect:


```

Suspicious File Access


↓

Unexpected Network Access


↓

System Modification


```



---

# Plugin Trust Level


Levels:



## Trusted


Official plugins.



---

## Verified


Reviewed third-party plugins.



---

## Unknown


User installed plugins.



---

## Blocked


Unsafe plugins.



---

# Plugin Installation Flow


```

Select Plugin Package


↓

Extract Temporary Files


↓

Read Manifest


↓

Security Scan


↓

Permission Confirmation


↓

Install


↓

Load


```



---

# Plugin Update System


## Purpose


Keep plugins compatible and secure.



---

# Update Sources


Support:


```

Local Package


↓

Official Repository


↓

Private Server


```



---

# Update Process


```

Check Version


↓

Download Package


↓

Verify Signature


↓

Backup Current Version


↓

Replace Files


↓

Restart Plugin


```



---

# Update Rollback


If update fails:



```

Restore Previous Version


↓

Enable Old Plugin


↓

Generate Report


```



---

# Plugin Version Backup


Before update:


Save:


```

Old Plugin Files


↓

Configuration


↓

State Data


```



---

# Plugin Removal System


## Purpose


Completely remove plugins.



---

# Remove Flow


```

Disable Plugin


↓

Stop Runtime


↓

Remove Files


↓

Remove Configuration


↓

Remove Database Records


```



---

# Removal Protection


Before deleting:


Check:


```

Plugin Running


↓

Dependent Plugins


↓

User Data


```



---

# Plugin Dependency Handling


Example:


Plugin B requires:


```

Plugin A


```



Removing A:


Show warning.



---

# Plugin Data Management


Each plugin owns:


```

Plugin Storage Area


```



Example:


```

Data


|

└── Plugins


    |

    └── PluginName


```



---

# Plugin Data Removal


Options:


```

Remove Plugin Only


↓

Keep User Data


↓

Delete Everything


```



---

# Plugin Logging System


Record:


```

Install


↓

Load


↓

Permission Request


↓

Execution


↓

Failure


↓

Removal


```



---

# Plugin Performance Monitoring


Monitor:



```

CPU Usage


↓

Memory Usage


↓

Execution Time


↓

Errors


```



---

# Plugin Health Score


Range:


```

0-100


```



---

# Health Factors


Include:


```

Crash Count


↓

Resource Usage


↓

Response Time


↓

Compatibility


```



---

# Automatic Protection


If plugin repeatedly fails:



```

Disable Plugin


↓

Notify User


↓

Generate Diagnostic Report


```



---

# Testing Requirements



## Interface Test


Verify:


Plugin API compatibility.



---

## Permission Test


Verify:


Unauthorized access blocked.



---

## Crash Test


Verify:


Core system survives plugin failure.



---

## Performance Test


Verify:


Resource limits.



---

## Update Test


Verify:


Rollback works.



---

# Plugin System Final Requirement


The plugin system must provide:


Flexible extension


↓

Strong isolation


↓

Permission control


↓

Safe third-party support


↓

Stable long-term operation



END OF DOCUMENT