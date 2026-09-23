# CleanMyPC Next Browser Cleaner

## Objective

Apply cache and privacy rules across installed browser profiles without guessing paths.

## Supported families

Chromium profiles include Chrome, Edge, Brave, Arc, Opera and Vivaldi; Firefox uses its native profile schema. Discover profiles from authoritative configuration, verify ownership and detect locks before mutation.

## Rules

Cache, code cache, GPU cache, service-worker cache, history, downloads and cookies are separate selectable scopes. Login state is preserved by default and passwords are out of scope.

## Acceptance

Use synthetic profiles to verify profile discovery, byte/count accounting, backup, time range, whitelist, locked database, cancellation and post-clean validation.

## Implementation dossier

- IDs: `LCN-BROWSER-CACHE-001..012`; inputs are browser provider, profile, cache categories, lock state, and time range.
- Pipeline: provider manifest → profile discovery → rule expansion → lock check → measurement → preview → clean → verification.
- Output separates cache-only findings, locked-file count, owner, estimated bytes, verified bytes, and restart requirement.
- Only current-user profiles are writable without elevation; other-account profiles are read-only or denied.
- Schema mismatch and locked files are itemized; browser databases are not deleted by default.
- Logs carry browser/version/profile, rule ID, lock state, skipped category, and verified freed bytes.
- Tests use Chromium, Firefox, Edge, and WebView fixtures with multiple profiles and active locks.
