# Test Plan

- Static: manifest has no `<uses-permission>` and no hidden/root API references.
- Build: local Gradle 8.10.2/JDK 17 runs `test assembleDebug assembleAndroidTest`.
- Reader: public PackageManager APIs, Android 13+ flags, sorted/deduplicated identity, typed failures, and per-package fault tolerance.
- Metadata: target SDK, last update time, enabled/debuggable facts are read-only and never treated as malware conclusions.
- Attention: only Debuggable, Disabled, or None signals are derived from observed flags; no deletion or remediation is triggered.
- Delta: app-private snapshots hash package identities and compare version/target SDK/update time/state fields; raw package names are not persisted.
- Audit: JSONL is escaped, app-private, flushed with `fd.sync()`, bounded to 200 records, and contains counts/flags rather than raw targets.
- Export: user confirmation precedes sharing; FileProvider exposes only cache exports; request and result are audited.
- UI: Chinese homepage hierarchy and navigation remain unchanged; results provide search, filters, virtualized rows, detail semantics, and explicit provider-visible scope.
- Instrumentation: `FileProviderContractTest` verifies cache-scoped URI generation and redacted export content.
- Device: shell-owned install and instrumentation passed on OnePlus PKG110/API 36; only serial `92f73a16` is in scope.
- Current local result: `test assembleDebug assembleAndroidTest` PASS after T-033/T-034 changes.
- Out of scope: other connected devices, other API/OEM matrices, destructive cleanup, hidden APIs, Root, and external share-handler success when no handler is installed.
