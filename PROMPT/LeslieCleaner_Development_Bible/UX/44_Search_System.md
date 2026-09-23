# CleanMyPC Next Search System

## Objective

Return accurate, explainable results across modules without broad rescans on every keystroke.

## Retrieval

Normalize Unicode compatibility forms, case, punctuation and spacing. Rank exact identity, prefix, token and contains matches; search names, publishers, paths, tags and evidence fields with source weighting.

## Safety

Debounce and cancel stale queries, deduplicate by stable identity and disclose whether data came from cache or live scan. Search never grants permission to delete.

## Acceptance

Test Chinese/English mixed text, punctuation, Unicode variants, publisher collisions, stale results, cancellation, pagination and permission boundaries.

## Implementation dossier

- IDs: `LCN-UX-SEARCH-001..012`; inputs are query, approved scope, filters, page, sort, and cancellation token.
- Matching normalizes Unicode FormKC, whitespace, punctuation, casing, and mixed Chinese/English fields.
- Ranking is exact identity → prefix → contains → metadata match; a publisher match cannot override conflicting product identity.
- Output includes result type, matched fields, score, freshness, permission state, and explanation of the match.
- Search is read-only, bounded to approved indexes/providers, cancellable, paged, and does not enumerate arbitrary disks on demand.
- Tests cover collisions, Unicode, stale results, cancellation, paging, denied scope, and relevance ordering.
