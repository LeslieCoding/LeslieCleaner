# Residual Engine Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent together with `Modules/24_Residual_Engine.md` and `00_Execution_Contract.md`.

## Mission

Implement or repair the CleanMyPC Next Residual Engine. The engine finds evidence-backed leftovers after an uninstall quickly and safely. It must not begin with recursive scanning of a whole disk, and it must not remove an item solely because a name happens to resemble an application.

## Required pipeline

Implement exactly this staged pipeline. Record duration, item count, source, and failure state for every stage.

1. **Install identity stage**: query uninstall records, MSI ProductCode/UpgradeCode, Appx/MSIX identity, package IDs, install location, uninstall string, and provider metadata.
2. **Known rules stage**: query versioned residual rules by normalized product/publisher identity. Exact product + publisher rules outrank generic publisher rules. Product-only rules are allowed only when the publisher is unavailable, and remain explainable.
3. **Install evidence stage**: inspect only bounded uninstall/install logs associated with the discovered app. Never parse arbitrary user documents or unrelated logs.
4. **Index stage**: query a fresh local index by publisher/product/path evidence. Retain index freshness and source metadata.
5. **Deep stage**: run only if prior evidence is insufficient, user opted in, roots are approved, and cancellation is available. Restrict to known roots such as program/application data locations. Do not traverse reparse points. Do not scan the entire volume.

## Identity and ranking rules

- Normalize product and publisher strings with Unicode FormKC, trim, collapse whitespace, and compare case-insensitively.
- A supplied product name is a strong identity signal. Reject a candidate with a conflicting product even if publisher matches.
- Exact product match plus absent publisher metadata may remain a lower-confidence candidate, but the UI must disclose that metadata is incomplete.
- Rank exact identity above known rule, install log, index, and deep-scan evidence.
- Deduplicate by canonical path/registry identity, retain all sources as evidence, and select the highest confidence classification.
- A stale rule, inaccessible path, missing log, or stale index must be recorded, not silently ignored.

## Residual item contract

Every result must include:

```text
FindingId, CanonicalIdentity, DisplayPath, Kind, SizeEstimate,
SourceStage, Evidence[], Confidence, Risk, AccessState,
RequiresElevation, CanClean, BackupRequired, RollbackAvailable,
RuleVersion, IndexFreshness, Explanation
```

`AccessState` is never inferred from size. Use `Verified`, `Inaccessible`, `Protected`, `Missing`, or `Stale` explicitly.

## Cleanup integration

The residual engine discovers; the Cleaner engine executes. Never delete directly from a scan loop. Convert approved findings to a current preview token, require confirmation and authorization, then invoke quarantine/backup plus the standard cleanup workflow. Registry entries use explicit hive/view backup and read-back verification.

For a folder finding, do not recursively enumerate a new unbounded tree during cleanup. Stay within the approved canonical root and preserve a manifest of every changed child.

## Logging and diagnostics

Emit `ResidualScan.Start`, `ResidualScan.StageStart`, `ResidualScan.StageFinish`, `ResidualScan.Candidate`, `ResidualScan.Merge`, `ResidualScan.DeepScanRefused`, `ResidualClean.Start`, `ResidualClean.Finish`, and `ResidualClean.Failure` events. Include operation ID, normalized identity, stage, counts, bytes, confidence, duration, permission, and failure kind. Redact personal paths in diagnostic exports.

## Acceptance tests

Create or retain deterministic tests for:

1. Exact product/publisher rule hit.
2. Product-only query without a publisher exception.
3. Conflicting product/publisher candidate rejection.
4. Unicode/whitespace normalization.
5. Generic publisher rule and product-specific rule precedence.
6. Corrupt uninstall log handling.
7. Stale index and inaccessible location reporting.
8. Case-insensitive deduplication retaining highest confidence and full evidence.
9. Deep scan opt-in/refusal, bounded roots, reparse-point refusal, and cancellation.
10. Quarantine/backup, read-back, rollback, and repeated cleanup idempotence.

## Performance targets

Measure and report per-stage P50/P95. Rules and identity stages must complete before index/deep scan. Deep scan has a visible separate progress state and may not run merely because the UI page loaded. Any size calculation that recursively enumerates a directory must be bounded, cancellable, and timed separately from discovery.

## Handoff

Return requirement IDs, pipeline stages modified, tests and counts, benchmark result, permission behavior, rollback behavior, stale/partial conditions, and any external Windows evidence still pending.
