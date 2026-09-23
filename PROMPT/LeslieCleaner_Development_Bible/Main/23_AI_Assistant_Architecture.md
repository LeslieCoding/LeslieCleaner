# LeslieCleaner Development Bible

Version:

1.0


Document Type:

AI Assistant Architecture Specification


Priority:

★★★★★



---

# Purpose


This document defines the architecture of the LeslieCleaner AI assistant module.


The AI assistant provides:


System analysis


↓

Intelligent recommendations


↓

Natural language explanation


↓

Optimization suggestions


↓

User assistance



---

# Core Design Philosophy


AI is an assistant, not an automatic system controller.



The AI module must:


Explain


↓

Recommend


↓

Assist



It must not:


Automatically modify critical system settings.



---

# Main Goals


The AI assistant must provide:


Smart analysis


↓

Easy understanding


↓

Safe recommendations


↓

Personalized suggestions



---

# Architecture Overview


```

AI Assistant


        |

        |

+----------------+

Context Collector

+----------------+

        |

        |

+----------------+

Knowledge Layer

+----------------+

        |

        |

+----------------+

AI Engine

+----------------+

        |

        |

+----------------+

Safety Filter

+----------------+

        |

        |

+----------------+

Response Generator

+----------------+


```



---

# Module Structure


```

AI


|

├── Context


├── Knowledge


├── Model


├── Prompt


├── Safety


├── Recommendation


├── History


```



---

# AI Capability Overview


The AI assistant supports:



## System Health Analysis


Analyze:


```

CPU Usage


↓

Memory Usage


↓

Disk Usage


↓

Startup Status


↓

Cleanup History


```



---

## Cleanup Recommendation


Generate:


```

Possible Cleanup


↓

Optimization Suggestions


↓

Storage Recommendations


```



---

## Startup Advice


Analyze:


```

Startup Items


↓

Impact Score


↓

Disable Recommendation


```



---

## Privacy Advice


Analyze:


```

Browser Privacy Status


↓

Tracking Risk


↓

Cleanup Suggestions


```



---

## User Question Answering


Support:


Natural language interaction.



Example:


User:


```

为什么电脑越来越慢？


```



AI:


```

可能原因：

1. 启动项增加

2. 临时文件增加

3. 磁盘空间不足


建议：

...

```



---

# AI Context Collector


## Purpose


Collect necessary system information.



---

# Context Sources


Include:



## Hardware


```

CPU


Memory


Disk


GPU


```



---

## System


```

System Version


System Uptime


Installed Software


```



---

## LeslieCleaner Data


```

Scan History


Cleanup History


Startup Records


```



---

# Privacy Rule


Context collection must follow:



```

Minimum Required Data


↓

Local Processing


↓

No Personal Content


```



---

# Forbidden Collection


Never collect:


```

Passwords


↓

Browser Password Database


↓

Personal Documents Content


↓

Private Messages


```



---

# AI Knowledge Layer


## Purpose


Provide domain knowledge.



---

# Knowledge Sources


Include:


```

System Optimization Rules


↓

LeslieCleaner Documentation


↓

Hardware Knowledge


↓

Software Knowledge


```



---

# Knowledge Storage


Options:



## Local Knowledge Base


Recommended.



Format:


```

JSON


↓

Markdown


↓

SQLite


```



---

# Knowledge Example


```json

{

 "topic":"startup",

 "rule":"high impact user software can be delayed"

}


```



---

# AI Engine


Supports:



## Local Models


Examples:


```

Qwen


↓

DeepSeek


↓

Llama


```



---

## Remote API


Optional:



```

OpenAI Compatible API


↓

Private API Server


```



---

# Model Adapter Layer


Purpose:


Hide model differences.



---

# Interface


```csharp

public interface IAIProvider

{

    Task<string>

    GenerateAsync(

        string prompt);


}

```



---

# Supported Providers


Example:


```

OpenAIProvider


↓

DeepSeekProvider


↓

LocalLLMProvider


```



---

# Local AI Priority


Recommended order:



```

Local Model


↓

Private API


↓

Cloud API


```



---

# Reason:


Privacy


↓

Cost Control


↓

Offline Capability



---

# AI Request Flow


```

User Request


↓

Collect Context


↓

Build Prompt


↓

AI Model


↓

Safety Check


↓

Generate Response


```



---

# Prompt Builder


## Purpose


Generate structured AI instructions.



---

# Prompt Structure


```

Role


↓

Context


↓

Task


↓

Restrictions


↓

Output Format


```



---

# Example


Role:


```

You are LeslieCleaner system optimization assistant.


```



Context:


```


16GB RAM


50GB free space


```



Task:


```

Analyze performance issues.


```



---

# AI Output Format


Preferred:


```

Problem


↓

Reason


↓

Suggestion


↓

Risk


```
---

# AI Health Score System


## Purpose


Provide an understandable computer health evaluation.



---

# Health Score Range


```

0 - 100


```



---

# Score Meaning



## 90-100


Excellent



---

## 70-89


Good



---

## 50-69


Needs Optimization



---

## 0-49


Attention Required



---

# Health Score Factors


Include:



## Storage Health


Factors:


```

Free Space


↓

Temporary Files


↓

Large Files


```



Weight:


30%



---

## Startup Health


Factors:


```

Startup Count


↓

High Impact Items


↓

Boot Time


```



Weight:


25%



---

## Privacy Health


Factors:


```

Tracking Cookies


↓

Browser Data


↓

Privacy Risk


```



Weight:


20%



---

## System Cleanliness


Factors:


```

Junk Files


↓

Residual Files


↓

Cache


```



Weight:


25%



---

# Score Calculation Example


```

Health Score =


Storage × 0.30


+

Startup × 0.25


+

Privacy × 0.20


+

Cleanliness × 0.25


```



---

# Recommendation Engine


## Purpose


Convert analysis results into actionable suggestions.



---

# Recommendation Model


```csharp

public class AIRecommendation

{

    public string Category { get; set; }


    public string Problem { get; set; }


    public string Reason { get; set; }


    public string Suggestion { get; set; }


    public int RiskLevel { get; set; }


}


```



---

# Recommendation Categories


Include:



```

Storage


↓

Performance


↓

Startup


↓

Privacy


↓

Security


```



---

# Recommendation Priority


Levels:



## Critical


Immediate attention.



Example:


Disk almost full.



---

## High


Recommended soon.



Example:


Large unnecessary files.



---

## Medium


Optimization suggestion.



Example:


Disable unused startup software.



---

## Low


Optional improvement.



---

# AI Decision Rules


Example:



Condition:


```

Free Space < 10%


```



AI:


```

Recommend cleanup


```



---

Condition:


```

Startup Count > 20


```



AI:


```

Analyze startup items


```



---

# Prompt Engineering System


## Purpose


Ensure stable AI responses.



---

# Prompt Template


```

SYSTEM:


Role Definition


↓

Rules


↓

Safety Restrictions



CONTEXT:


System Information


↓

User Data Summary



TASK:


Required Analysis



OUTPUT:


Response Format


```



---

# System Prompt Requirements


Must include:



```

You are LeslieCleaner AI Assistant.


You provide suggestions only.


Do not execute dangerous operations.


Explain risks clearly.


```



---

# AI Safety Rules


The AI must:



Never:


```

Delete files directly


↓

Modify registry directly


↓

Disable security software


↓

Change system protection


```



---

# AI Permission Model


Three levels:



## Information


AI can:


Analyze and explain.



---

## Recommendation


AI can:


Suggest actions.



---

## Execution


Requires:


User confirmation.



---

# Dangerous Operation Protection


Before:


```

Registry Repair


↓

Startup Disable


↓

Forced Cleanup


```



AI response must include:


```

Risk Explanation


↓

Backup Suggestion


↓

Confirmation Requirement


```



---

# Local Model Integration


## Purpose


Support offline AI.



---

# Supported Model Types


Examples:


```

Qwen


↓

DeepSeek


↓

Llama


```



---

# Local Model Architecture


```

LeslieCleaner


↓

AI Adapter


↓

Local Runtime


↓

Model


```



---

# Local Runtime Examples


Support:



```

llama.cpp


↓

Ollama


↓

Local API Server


```



---

# Model Requirements


Recommended:


Small Models:


```

3B-8B


```



---

# Hardware Adaptation


For:


16GB RAM



Recommended:


Quantized models.



Example:


```

Q4


Q5


```



---

# API Provider Architecture


Support OpenAI-compatible API.



---

# Provider Interface


```csharp

public interface IAIClient

{

    Task<string>

    ChatAsync(

        string message);


}

```



---

# Provider Examples


```

OpenAIClient


DeepSeekClient


KimiClient


LocalClient


```



---

# AI Configuration


Store:



```

Provider


↓

Model


↓

Endpoint


↓

Token


↓

Temperature


```



---

# Sensitive Data Protection


API keys must:



Never:


Store plaintext.



---

# Recommended:


System Credential Manager.



---

# AI History System


Purpose:


Save useful conversations.



---

# History Content


Store:


```

Question


↓

AI Response


↓

Context Summary


↓

Time


```



---

# History Privacy


Never store:


```

Personal Content


↓

Passwords


↓

Private Files


```



---

# AI Logging


Record:


```

Request Time


↓

Model


↓

Response Time


↓

Error


```



---

# Error Handling


Handle:


```

API Timeout


↓

Model Failure


↓

Invalid Response


↓

Network Error


```



---

# AI Performance Target


Response:


Local model:

<10 seconds



API:

<5 seconds



---

# Testing Requirements



## Prompt Test


Verify:


Stable answers.



---

## Safety Test


Try:


Dangerous requests.



---

## Model Test


Test:


Different providers.



---

## Offline Test


Verify:


Local AI operation.



---

# AI Assistant Final Requirement


The AI assistant must provide:


Intelligent analysis


↓

Safe recommendations


↓

Private processing


↓

Natural interaction


↓

Reliable assistance



END OF DOCUMENT


---