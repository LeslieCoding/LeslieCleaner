# Registry Engine Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent together with `Modules/22_Registry_Cleaner.md` and `00_Execution_Contract.md`.

## Mission

Implement or repair registry scanning and cleanup for CleanMyPC Next. The engine must be precise, view-aware, recoverable, and permission-safe. A registry row is not actionable merely because it matches text; it needs rule evidence, risk assessment, policy approval, backup, and read-back verification.

## Mandatory registry identity

Every registry resource is identified by:

```text
Hive, PhysicalView, SubKeyPath, ValueName, ValueKind, CanonicalIdentity
```

Never use merged `HKCR` behavior when scanning or deleting. Open `HKCU\Software\Classes` and `HKLM\Software\Classes` explicitly with `Registry32` or `Registry64`. A 32-bit and 64-bit result are separate physical targets even when their display paths resemble each other.

## Scan policy

1. Start only from versioned rules and explicitly selected bounded branches.
2. Gather exact evidence: matching pattern, referenced file/package state, source view, owner/protection status, and risk reason.
3. Normalize case for matching but preserve actual stored casing for display and restore.
4. Classify findings as verified, candidate, protected, inaccessible, missing, stale, or denied.
5. Never enumerate the entire registry as a default scan path.

Rules must state hive/view applicability, path pattern, value pattern, evidence predicate, risk, default action, exclusions, and test fixture. A pattern without an exclusion for protected Microsoft/security paths cannot be enabled.

## Permission and protection policy

- Evaluate protection before opening writable handles.
- Windows Resource Protection, policy, security, integrity, boot, service, driver, and protected Microsoft targets are denied by default.
- HKLM and ACL-protected paths require typed elevation through the security gate; UAC refusal remains a visible denied state.
- Do not change ACLs merely to make deletion succeed unless a separately authorized repair feature owns that requirement.
- An access-denied result must identify hive, view, canonical path, action, and safe next action without exposing secrets.

## Backup and mutation protocol

For every key or value mutation:

1. Reopen the exact hive/view read-only and verify the precondition.
2. Capture a backup containing hive, view, key path, original value name/casing, kind, data, operation ID, timestamp, and integrity hash.
3. Verify the backup can be read before obtaining a writable handle.
4. Open the parent/key in the same physical view with the minimum writable scope.
5. Delete only the approved key/value; flush if supported.
6. Reopen read-only and read back the target with case-insensitive name matching.
7. If verification fails, attempt restore and return `RolledBack` or `Failed`—never `Committed`.

Repeated deletion of a verified missing target is idempotent and returns a truthful `NotFound/AlreadyAbsent` result, not an exception-driven failure.

## Logging

Emit `RegistryScan.Start`, `RegistryScan.Finding`, `RegistryDelete.Start`, `RegistryDelete.BackupVerified`, `RegistryDelete.Decision`, `RegistryDelete.ReadBack`, `RegistryDelete.Rollback`, and `RegistryDelete.Failure`.

Required fields: operation/requirement ID, hive, view, canonical path, value name/kind, rule ID, risk, policy decision, elevated state, backup hash, read-back result, duration, and failure kind. Do not log raw sensitive values.

## Tests

Add/maintain tests for:

1. HKCU/HKLM and Registry32/Registry64 physical separation.
2. Value-name casing capture and case-insensitive deletion/read-back.
3. Missing target and repeat invocation idempotence.
4. Protected policy denial before mutation.
5. Access denial and declined elevation guidance.
6. Corrupt or unverified backup refusal.
7. Read-back failure triggering restore.
8. Exact key/value scope; sibling keys/values remain intact.
9. Cancellation before mutation and between batch items.
10. Structured log fields for both success and failure.

## Handoff

Report the precise hive/view coverage, rules changed, permission boundary, backup/rollback evidence, tests and counts, plus any all-users/UAC Windows matrix still pending.
