# LeslieCleaner Android Development Bible - 23_AI_Assistant_Architecture

> Android Prompt ID: AP-MAIN-23-AI-ASSISTANT-ARCHITECTURE-MD  
> Source Prompt: `Main\23_AI_Assistant_Architecture.md`  
> Target platform: Android 10+ (API 29+)  
> Primary focus: On-device/local-first advisory AI, redaction, network opt-in, structured output validation and safe action delegation  
> Document status: generated Android adaptation; implementation evidence must be appended as work lands.

## 1. Objective and translation boundary

This document is the Android implementation contract corresponding to the source prompt above. It
must be read together with `ANDROID_MASTER_CONTRACT.txt` and the Android project manifest. Every
desktop-only assumption must be translated into an Android-supported capability or explicitly
marked `UnsupportedOnAndroid`; it is forbidden to imitate Windows registry, service-control, driver-
store, or unrestricted filesystem behavior with undocumented hacks.

The feature focus for this document is **On-device/local-first advisory AI, redaction, network opt-in, structured output validation and safe action delegation**. The result must be useful on stock Android,
degrade safely on vendor-modified Android, and never require root, hidden APIs, bootloader unlock,
or an undocumented permission merely to complete a scan.

## 1A. LAEP execution contract

This document is executed through the LAEP cycle: Read -> Analyze -> Plan -> Implement -> Build ->
Test -> Debug -> Retest -> Regression -> Document -> Continue. Before editing, restore
`Android_App/AI_SPEC/24_PROJECT_STATE.md`, `25_PROGRESS.md`, `26_DECISION_LOG.md` and
`27_KNOWN_ISSUES.md`. Every change must identify requirement IDs, risk, rollback, commands, results
and remaining evidence. Device access is a gated operation: do not connect to or mutate a device
until the APK/package and local verification gates declared in PROJECT_STATE have passed.

## 2. Required architecture

- Kotlin, Jetpack Compose/Material 3 for presentation, and a lifecycle-aware ViewModel per screen.
- Clean boundaries: `app` (bootstrap/navigation), `feature-*` (UI/use cases), `domain` (typed
  policies and results), `data` (Room/DataStore/repositories), and `platform` (PackageManager,
  Storage Access Framework, MediaStore, UsageStats, WorkManager and optional ADB test adapter).
- Kotlin coroutines with structured concurrency. No GlobalScope, blocking calls on the main thread,
  or retained Activity/Context references in domain objects.
- Every platform operation returns a typed result with `success`, `failureKind`, `requiresUserAction`,
  `changedCount`, `verified`, `rollbackState`, and a correlation/operation ID.
- Build variants are `debug`, `benchmark`, and `release`; release must not include test endpoints,
  permissive network trust, or destructive mock fallbacks.

## 3. Android permissions and trust model

| Capability | Default | Required user action | Prohibited shortcut |
|---|---|---|---|
| App/package inventory | allowed through PackageManager | none beyond normal install | hidden package APIs |
| Shared files | scoped storage | SAF directory/file grant when needed | MANAGE_EXTERNAL_STORAGE by default |
| Media cleanup | MediaStore + recoverable delete | system confirmation where required | direct provider database writes |
| Usage/launch evidence | optional | Usage Access settings screen | accessibility abuse |
| Notifications | optional | POST_NOTIFICATIONS on API 33+ | silent background spam |
| Network/AI | opt-in | explicit provider and privacy consent | uploading raw paths, tokens, or content |
| ADB board tests | test-only | USB debugging and RSA approval | production dependency on ADB |

Permission denial, revocation, restricted profiles, work profiles and OEM policy changes are normal
typed outcomes. Do not retry in a loop. Explain the minimum user action and preserve all scan data.

## 4. Scan accuracy and target identity

All findings must carry a stable typed identity: package name plus user/profile, URI plus persistable
grant, MediaStore ID, or an app-owned database key. Display strings are never used as delete keys.
Normalize case only where the Android API is case-insensitive; preserve URI encoding and authority.
Deduplicate by physical identity, retain the strongest risk evidence, and record provider/source,
API level, profile, permission state and timestamp. A cached result is usable only when its provider
fingerprint and permission snapshot still match the current device.

## 5. Backup, mutation, read-back and rollback

No destructive operation is valid without a preflight snapshot or a platform-supported reversible
transaction. The sequence is mandatory:

1. Create an operation ID and durable manifest.
2. Resolve and re-check the exact target using the current profile/user and provider.
3. Capture metadata/content that Android legally permits, hash it, and verify the snapshot.
4. Ask for confirmation when the risk policy requires it.
5. Execute through the public Android API (PackageInstaller, MediaStore recoverable delete, SAF, or
   app-owned transaction).
6. Re-query the same provider and verify the expected absence/change; never infer success from a
   returned `Intent` alone.
7. On failed verification, stop further mutation and attempt rollback; report `Partial` or
   `VerificationFailed` rather than claiming success.

Irreversible operations are isolated behind an explicit second confirmation and are never included
in a bulk action by default.

## 6. Logging and diagnostics

Write structured JSONL events to the app-private files directory and expose an exportable diagnostic
bundle. Every scan/mutation records `TimestampUtc`, `OperationId`, `Feature`, `Provider`, `ApiLevel`,
`ProfileId` (redacted), `PermissionState`, `Decision`, `Result`, `Verified`, `FailureKind`, latency,
and counts. Never log raw personal paths, file contents, account identifiers, OAuth tokens, or ADB
serials without redaction. Use atomic append/rotation, bounded retention, crash-safe flush for
destructive events, and a self-diagnostic sink when the primary file is unavailable.

## 7. Lifecycle and concurrency

Cancellation is propagated from the UI and WorkManager. If cancellation occurs after mutation has
started, finish a bounded read-back/rollback attempt with a non-cancellable cleanup token, then
return `CancelledAfterMutation` with truthful verification state. Screen rotation must preserve the
operation ID and observe the same state rather than starting a second operation. A per-target or
per-profile mutex prevents duplicate taps and stale selections.

## 8. Testing and acceptance

- JVM unit tests for identity normalization, deduplication, policy, risk and typed failures.
- Robolectric/instrumentation tests for PackageManager, MediaStore, SAF grants, DataStore/Room and
  lifecycle restoration.
- Device tests on API 29, 30, 33 and 35 where available, including work profile and permission
  denial paths. ADB tests are read-only unless a disposable fixture and explicit test flag exist.
- Mutation tests must assert backup hash, exact target, post-operation provider read-back, rollback
  behavior, cancellation-after-mutation and durable log events.
- Performance tests must record cold/warm scan latency, allocations, battery impact and provider
  query counts; no full shared-storage walk is permitted when a scoped rule can locate the target.

Acceptance is not "the button returned." It requires `verified=true`, an inspectable operation log,
and a reproducible test artifact or an explicit `PendingEvidence` note for device-only behavior.

## 9. Implementation checklist

- [ ] Add/confirm requirement IDs `AP-MAIN-23-AI-ASSISTANT-ARCHITECTURE-MD` in the Android traceability matrix.
- [ ] Implement the domain contract and Android platform adapter separately.
- [ ] Add permission-denied, empty-result, cancellation and OEM-error branches.
- [ ] Add backup/hash/read-back/rollback tests for every mutation path.
- [ ] Add structured start/decision/result/failure logs and redaction tests.
- [ ] Add Compose semantics, TalkBack labels, large-font and dark-theme checks where UI applies.
- [ ] Record build, test, device/API, unresolved issue and evidence paths in `DEVELOPMENT_LOG.txt`.

## 10. AI execution prompt

Implement only the scope of this document. Before coding, inspect the current Android module and
traceability matrix. Do not invent Windows-only APIs. Keep changes small and reversible, add tests
before widening permissions, and report exact files, commands, test counts, device/API evidence,
known limitations and rollback behavior. If Android cannot legally or reliably provide a capability,
return a typed unsupported result and document the user-visible alternative.

## 11. Source prompt retained for traceability

The following is the source document that this Android adaptation translates. Its requirements may
be expanded for Android but must not be silently dropped:

<details>
<summary>Original source content</summary>

```markdown
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
```
</details>

## 12. Change evidence

| Date | Change | Source/test evidence | Status |
|---|---|---|---|
| 2026-09-08 | Android adaptation generated from the corresponding source prompt | `DEVELOPMENT_LOG.txt`, `PROMPT_MANIFEST.json` | Documentation baseline |

