# CleanMyPC Next Smart Scan Module

## Objective

Provide a fast, explainable overview of reclaimable data without claiming that estimates are actual deletion results.

## Pipeline

Run rule groups for temporary files, logs, crash dumps, shader/font caches, update caches, delivery optimization, thumbnails, prefetch, recycle bin and AI application caches. Each rule declares scope, source, risk, measurement method, exclusion policy and cancellation behavior. Known locations are enumerated directly; deep traversal is opt-in.

## Output

Return item count, logical bytes, allocated bytes, inaccessible count, source, risk and `CanDelete`. Deduplicate by canonical identity and retain measurement discrepancies.

## Acceptance

Fixture tests cover duplicate paths, reparse points, inaccessible files, cancellation, stale cache and post-clean verification. UI displays estimated versus verified freed bytes separately.

## Implementation dossier

- IDs: `LCN-SCAN-SMART-001..008`; inputs are selected categories, user scope, rule version, and cancellation.
- Pipeline: rule manifest → provider enumeration → normalization → deduplication → risk classification → preview.
- Output includes estimated/verified bytes, inaccessible count, confidence, source rule, and freshness.
- User scope is default; machine scope is read-only unless the security gate grants access.
- Provider failure is visible as partial coverage, never as zero bytes.
- Logs: `SmartScan.Start`, `Provider`, `Finish`, `Failure`, with category and P95 timing.
- Tests cover every rule group plus stale rules, duplicate identities, reparse points, cancellation, and denied roots.
