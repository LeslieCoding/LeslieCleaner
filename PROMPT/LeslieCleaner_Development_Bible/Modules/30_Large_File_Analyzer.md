# CleanMyPC Next Large File Analyzer

## Objective

Explain where storage is used and produce actionable, non-destructive recommendations.

## Analysis

Use bounded directory enumeration, filesystem allocation size, file type and age. Build Top-N and treemap data incrementally; skip protected and reparse escapes and retain inaccessible counts.

## Interaction

Filtering, sorting and pagination operate on indexed records rather than rescanning the disk. Recommendations never delete automatically and disclose logical versus allocated size.

## Acceptance

Test cancellation, large directories, reparse loops, permission failures, duplicate hard links, paging accuracy and recommendation evidence.

## Implementation dossier

- IDs: `LCN-LARGE-FILE-001..008`; inputs are bounded roots, size threshold, index freshness, paging, sorting, and exclusions.
- Output includes logical and allocated size where available, hard-link identity, access state, paging metadata, and recommendation evidence.
- Inaccessible roots are reported; opening a location is non-destructive and does not imply write permission.
- Stale index, sparse file, reparse point, and changing file are distinct states.
- Logs include root scope, index freshness, item count, bytes, page timing, and skipped-root reason.
- Tests cover sparse files, hard links, large folders, stale index, paging, cancellation, and denied roots.
