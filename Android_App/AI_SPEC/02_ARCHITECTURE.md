# LeslieCleaner Android 3.0.0 — Architecture

## Boundary diagram

```text
Compose UI
   │ state/events only
MaintenanceCenter ── AnalysisCoordinator ── AuditLogger
   ├── PackageInventoryReader ── PackageManager
   ├── DeviceHealthReader ───── StatFs / ActivityManager / BatteryManager
   ├── JunkCandidateReader ──── app-private cache / public storage facts / system handoff
   ├── SecuritySignalReader ─── PackageManager facts / Play Protect handoff
   ├── StartupSignalReader ──── public receiver declarations / settings handoff
   ├── RuleUpdateStore ──────── bundled signed/versioned rules / offline fallback
   ├── InventorySnapshotStore ── app-private hashed snapshot
   └── AnalysisRules ─────────── pure fact-to-signal rules
```

## Dependency rules

- UI may depend on coordinator contracts and immutable result models.
- Readers may depend on Android public APIs but never on Compose.
- Rules are pure Kotlin and never access Android or files.
- Snapshot and audit storage own their file formats and expose bounded operations.
- No module may call deletion, shell commands, Root, or hidden APIs.

## Analysis sequence

1. UI emits a single scan intent.
2. Coordinator rejects a duplicate while one run is active.
3. Background readers collect independent facts with per-reader fault tolerance.
4. Rules classify observations and preserve unavailable/error states.
5. Snapshot store compares hashed application facts and stores the new bounded snapshot.
6. Audit logger writes a redacted start/finish/failure record.
7. UI renders immutable module results and offers detail/history/export/system handoffs.

## Compatibility strategy

Use API 33 flag overloads with API 29+ fallback. Guard every optional metric. A missing metric is
`Unavailable`, not zero and not a success claim. Public API behavior is the only supported coverage.

## Performance strategy

Reader work runs on `Dispatchers.IO`; application rows use `LazyColumn`; persisted records are
bounded; no full-disk traversal is added. Instrumentation records duration and item counts.

## Security/privacy strategy

No permissions are added. Package identities are hashed in repeat-scan storage. Audit metadata stores
counts, statuses, duration, and safe classifications, not paths, URIs, account data, or raw package
names. Clear-history remains confirmed and app-private.

## Cloud update boundary

Rule updates are data-only, signed/versioned, and verified before activation. A configured endpoint,
certificate policy, privacy disclosure, and backend ownership are prerequisites for live cloud mode.
Until those are supplied and pass a new REG, the app uses bundled rules and reports cloud mode as
unconfigured; it never invents a server or silently uploads device data.
