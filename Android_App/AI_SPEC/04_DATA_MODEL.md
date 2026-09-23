# LeslieCleaner Android 2.0.0 — Data Model

## Observation model

- `InstalledAppInfo`: typed package identity, label, version, target SDK, update time, system/user,
  enabled state, debuggable state.
- `DeviceHealthSnapshot`: storage, memory, battery observations; every field carries availability
  and source semantics.
- `AnalysisResult`: completed, partial, or failed status; application summary; health snapshot;
  delta; duration; coverage.
- `InventoryDelta`: added, removed, changed, hasPrevious; never raw names.
- `InventoryAttention`: `Debuggable`, `Disabled`, `None`; not a malware score.

## Persistence

- `inventory-snapshot.dat`: app-private, bounded, hashed identity key plus normalized observed facts.
- `audit-events.jsonl`: app-private UTF-8 JSONL, max 200 records, redacted metadata.
- Export: cache-scoped FileProvider URI, explicit confirmation, text/plain.

## Migration

The existing snapshot format is versionless `hash|value`. 2.0.0 must either parse old values as
legacy version records or atomically replace them after a successful read. A failed migration keeps
the old file and marks delta as unavailable; it never deletes user files.
