# LeslieCleaner Android 3.0.0 — User Requirements

Original request source: USER_EXPLICIT

## Explicit requirements

| ID | Requirement | Classification | Acceptance |
|---|---|---|---|
| R3-001 | Refer to good Android/mobile cleaner modules and rework the app into a substantially richer major version. | USER_EXPLICIT | Research-backed module matrix and integrated 3.0.0 task tree exist. |
| R3-002 | Add junk/cache management suitable for Android. | USER_EXPLICIT | Shows safe measurable categories and user-confirmed/system-mediated cleanup routes; no fake full-device deletion. |
| R3-003 | Add virus/security cleaning capability. | USER_EXPLICIT | Local security checklist plus Play Protect handoff; wording never claims unsupported full malware removal. |
| R3-004 | Add cloud update capability after security analysis. | USER_EXPLICIT | Offline-first signed-rule update contract is implemented or explicitly gated; no unknown endpoint is contacted. |
| R3-005 | Add phone startup/autostart management. | USER_EXPLICIT | Shows boot receiver/app startup signals and opens user/system settings; no unauthorized toggling. |
| R3-006 | Synchronize system acceleration with memory and junk management. | USER_EXPLICIT | Acceleration screen combines memory facts, storage pressure, and safe next actions. |
| R3-007 | Add the common modules seen in reputable phone maintenance tools. | USER_EXPLICIT | Functional modules include junk, security, startup, acceleration, app management, health, and history. |
| R3-008 | Keep all device tests on the connected OnePlus phone only. | USER_EXPLICIT | Every device command uses serial 92f73a16; no other device is touched. |

## Derived requirements

| ID | Requirement | Classification | Acceptance |
|---|---|---|---|
| R3-101 | Build one maintenance center with separate module entry points while preserving the current Chinese visual language. | USER_INFERRED | Existing hierarchy remains recognizable; module cards no longer lead only to app search. |
| R3-102 | Use a typed candidate model for junk items with category, size, source, confidence, action route, and confirmation requirement. | AI_RECOMMENDED | Candidate previews never silently delete and show why each item is listed. |
| R3-103 | Use Play Protect/system settings as security authority and clearly label local heuristic signals. | AI_RECOMMENDED | Security result distinguishes platform status, local evidence, and unavailable data. |
| R3-104 | Treat cloud rules as signed/versioned/rollbackable data, not executable remote code. | AI_RECOMMENDED | Invalid, stale, unsigned, or unavailable update falls back to bundled rules. |
| R3-105 | Represent autostart as observable declarations and OEM handoff availability, not universal control. | AI_RECOMMENDED | Detail explains Android/OEM limitation and provides user-controlled route. |

## Non-functional requirements

- R3-N01: no Root, hidden APIs, QUERY_ALL_PACKAGES, accessibility abuse, silent deletion, or remote commands.
- R3-N02: network/cloud is opt-in, endpoint-configured, TLS-only, signature-verified, and offline-safe; no production endpoint is invented.
- R3-N03: all destructive or system-mediated actions require explicit user confirmation and read-back where possible.
- R3-N04: every module has unit tests, failure states, Chinese copy, evidence, and rollback documentation.
- R3-N05: OnePlus API 36 is the only real-device acceptance environment for this release.
