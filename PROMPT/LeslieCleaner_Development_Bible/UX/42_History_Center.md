# CleanMyPC Next History Center

## Objective

Make scans, cleanups, restores, updates and security events searchable and auditable.

## Data

Show UTC/local time, module, operation ID, scope, counts, bytes, result, failure kind, backup and restore links. Sensitive values and raw credentials never appear.

## Interaction

Use indexed paging, filters and export with redaction. A history item can open evidence or initiate a safe restore, never silently repeat deletion.

## Acceptance

Test ordering, pagination, retention, corrupt record handling, redaction, export integrity and restore navigation.

## Implementation dossier

- IDs: `LCN-UX-HISTORY-001..009`; history records operation ID, requirement ID, scope, evidence, result, backup, rollback, and timestamps.
- Records are append-oriented, paged, retention-managed, redacted, and protected from corrupt-record failure propagation.
- Detail view shows per-item outcomes, not only totals, and links to verified restore when available.
- Export contains schema version, integrity metadata, redaction status, and no credentials or raw personal documents.
- Tests cover order, pagination, retention, corrupt records, export, redaction, and restore navigation.
