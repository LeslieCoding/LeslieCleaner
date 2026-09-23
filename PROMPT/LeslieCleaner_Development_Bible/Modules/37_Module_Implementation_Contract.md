# Module Implementation Contract

Version: 2.0  
Status: Normative

This contract is shared by every file in `Modules/20–36`. Each module file supplies domain-specific rules; this document supplies the implementation depth that every module must meet.

## Module dossier

Every module MUST maintain a dossier with:

1. Objective and user stories.
2. Requirement IDs and priority.
3. Inputs, outputs, and domain models.
4. Discovery providers and source precedence.
5. Permission matrix.
6. Rule/configuration schema and versioning.
7. Pipeline stages and cancellation points.
8. Safety classification and protected targets.
9. Backup/quarantine/rollback behavior.
10. Logs, history records, notifications, and metrics.
11. Unit, integration, UI, performance, and release tests.
12. Known limitations and recovery instructions.

## Provider precedence

Providers are ordered from authoritative to heuristic. The module MUST retain the provider name and timestamp on each finding. A lower-confidence provider can add evidence but cannot silently downgrade a higher-confidence classification.

Provider failures are isolated. If one provider is unavailable, the result is marked partial and the user sees the missing source; the module does not claim a complete scan.

## Rule schema

Rules are versioned records with `RuleId`, `Module`, `Provider`, `Platform`, `Scope`, `Pattern`, `Risk`, `DefaultAction`, `MinConfidence`, `Exclusions`, `IntroducedVersion`, `LastVerifiedUtc`, and `SignatureStatus`.

Rules MUST be deterministic, reviewable, and testable without a live user profile. A rule that can match a protected operating-system path MUST include an explicit negative test.

## Result model

Every finding contains:

```text
FindingId, CanonicalIdentity, DisplayPath, Kind, Source, Evidence,
Confidence, Risk, SizeEstimate, AccessState, CanAct, RequiresElevation,
BackupRequired, RollbackAvailable, RuleVersion, CreatedUtc
```

The module MUST distinguish `NotFound`, `Inaccessible`, `Protected`, `Stale`, `Candidate`, and `Verified`. An inaccessible item is never silently treated as absent.

## Execution pipeline

`ValidateInput → Discover → Normalize → Deduplicate → Classify → Preview → Confirm → Authorize → Backup/Quarantine → Execute → ReadBack → PersistHistory → Notify`.

Each stage has a cancellation checkpoint and structured timing. Execution accepts only findings produced by the current operation or a signed, validated preview token; arbitrary paths supplied by the UI are rejected.

## Permission matrix

| Action | Standard user | Elevated helper | Blocked by default |
|---|---|---|---|
| Read user-scoped data | Yes, within scope | Optional | Outside approved roots |
| Read machine-scoped data | Best effort | Yes | Protected/system secrets |
| Move to quarantine | Yes if destination is writable | Yes | Protected artifacts |
| Delete registry or machine files | No unless policy permits | Yes with gate | WRP/security/integrity targets |
| Stop service/task/process | No by default | Explicit confirmation | Security and OS-critical targets |

## Acceptance matrix

Each module MUST have fixtures for clean machine, known leftovers, stale data, duplicate results, access denial, locked resource, protected resource, cancellation, and rollback. Test reports include counts, timings, environment, and artifact paths.

## Module-specific expansion

The following files MUST add their own acceptance details on top of this contract:

- `20_Smart_Scan`: category totals, estimated/verified bytes, and scan freshness.
- `21_Cleaner`: quarantine manifest, read-back, and partial failure presentation.
- `22_Registry_Cleaner`: physical 32/64-bit views, hive identity, ACL/UAC, and value casing.
- `23_Uninstaller`: provider selection, exit-code policy, process lifetime, and residual handoff.
- `24_Residual_Engine`: identity scoring, known rules, install logs, index evidence, and bounded deep scan.
- `25_Startup_Manager`: persistence locations, signature, delayed startup, and reversible disable.
- `26_Privacy_Guard`: browser/profile scope, cookie/session safety, and consent boundaries.
- `27_Browser_Cleaner`: provider version, locked databases, cache-only default, and browser restart warning.
- `28_File_Shredder`: algorithm, overwrite limits, SSD caveat, and verification result.
- `29_Duplicate_Finder`: hash tiers, collision policy, protected originals, and user selection.
- `30_Large_File_Analyzer`: index freshness, sparse files, hard links, and paging.
- `31_Service_Manager`: service dependencies, recovery settings, and boot-critical refusal.
- `32_Driver_Manager`: package identity, signed driver evidence, rollback and reboot requirement.
- `33_Task_Scheduler`: task XML identity, trigger preservation, and elevation boundary.
- `34_Extension_Manager`: host/profile ownership, signature, and reversible disable.
- `35_AI_Assistant`: evidence-only context, redaction, typed response, and no execution authority.
- `36_Settings`: schema migration, atomic writes, corrupt-config recovery, and reset behavior.
