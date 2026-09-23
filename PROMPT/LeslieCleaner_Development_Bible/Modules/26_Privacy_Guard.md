# CleanMyPC Next Privacy Guard

## Objective

Remove selected privacy traces without touching credentials or unrelated personal content.

## Scope

Support recent files, clipboard/search/run history, Office history, browser privacy data, location and voice caches through explicit rules. Cookies provide Keep All, Tracking Only, Selected Domains and whitelist modes. Password stores and login databases are never modified.

## Transaction

Detect running applications, create a verified database backup, execute one SQLite transaction per profile and validate counts afterward. Session data requires a separate explicit confirmation.

## Acceptance

Test time ranges, tracking-domain boundaries, whitelist behavior, locked databases, rollback, cancellation, no-confirmation rejection and credential preservation.

## Implementation dossier

- IDs: `LCN-PRIVACY-GUARD-001..010`; inputs are profile, category, time range, session policy, and confirmation.
- Output includes findings, protected-session count, backup state, deletion result, and rollback availability.
- Cache and logs are the default scope; cookies, tokens, passwords, active sessions, and credential stores are protected.
- Profile ownership is verified before access; cross-account locations are denied rather than guessed.
- Locked database, unsupported schema, and unavailable profile result in partial coverage, never a false empty result.
- Logs store provider/profile/category, time range, protected count, redaction state, and operation ID.
- Tests cover active sessions, locked files, multiple profiles, time filtering, backup/restore, cancellation, and credential preservation.
