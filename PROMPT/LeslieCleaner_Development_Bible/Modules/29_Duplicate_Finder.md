# CleanMyPC Next Duplicate Finder

## Objective

Find byte-identical and visually similar files while minimizing I/O and false matches.

## Algorithm

Group by size, compare bounded partial hashes, then compute full SHA-256 only for candidates. Images may use a perceptual hash as a separate similarity result; video/audio similarity never implies byte identity.

## Operations

Present one keeper recommendation with reason, allowlist and confidence. Deletion always routes through backup/quarantine and never removes all members of a group automatically.

## Acceptance

Test hard links, case variants, partial collisions, inaccessible files, cancellation, image similarity thresholds and rollback.

## Implementation dossier

- IDs: `LCN-DUP-FINDER-001..010`; inputs are bounded roots, exclusions, minimum size, hash tier, and protected-file policy.
- Pipeline: metadata group → partial hash → full hash → hard-link detection → risk classification → user selection.
- Output includes duplicate groups, original suggestion, hash evidence, wasted bytes, and chosen action.
- Roots are canonicalized; reparse traversal is disabled by default and hard links are never counted as independent bytes.
- Changed-during-scan, hash-collision suspicion, inaccessible items, and cancellation remain visible in each group.
- Logs include root scope, hash tier, group count, bytes, skipped count, and verification result.
- Tests cover true duplicates, same-size differences, hard links, reparse points, changed files, rollback, and selection persistence.
