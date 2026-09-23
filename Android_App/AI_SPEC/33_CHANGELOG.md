# Changelog

## 2.0.0 — 2026-09-09 — READY FOR REVIEW

- Rebuilt the project specification through LAEP 4.1 REG with a new Engineering Baseline,
  Assumption Register, Requirement Traceability, invariants, architecture, data model, task tree,
  test strategy, acceptance criteria, and evidence register.
- Added public Android device health observation models/readers for storage, memory, and battery.
- Added a background analysis coordinator that combines inventory, health, hashed repeat-scan delta,
  duplicate-run protection, typed failure, and duration.
- APK version is versionCode 6/versionName 2.0.0; OnePlus-only device regression and FileProvider instrumentation passed.
- Added a v2 snapshot format header with backward-compatible parsing of legacy 1.x snapshot lines.

## 1.0.0 — 2026-09-09

- Upgraded the PackageManager inventory from a count-only foundation to classified read-only analysis.
- Added system/user, enabled/disabled, and debuggable application counts.
- Added scan duration and bounded classification metadata to audit records while preserving old JSONL records.
- Completed privacy signals and preferences modules, including confirmed local audit-history clearing.
- Kept the existing Chinese UI visual system, navigation structure, no-permission posture, and FileProvider export boundary.
- Verified on OnePlus PKG110 / Android 16 API 36: 253 provider-visible applications; FileProvider instrumentation 1/1 passed.

Known limitation: external sharing depends on a share handler installed on the device.

## 1.1.0 — 2026-09-09

- Added application-name and package-name search inside scan results.
- Added all/user/system application filters.
- Added read-only per-application detail view for package, version, type, enabled state, and debug state.
- Replaced eager result rendering with a virtualized `LazyColumn` and added accessibility click descriptions.
- Kept the existing homepage UI visual system, permissions posture, audit boundary, and no-mutation policy.

## 1.1.1 — 2026-09-09

- Added privacy-preserving repeat-scan delta analysis using hashed package identities.
- Audit metadata now records added, removed, and changed counts for each verified scan.
- Verified on OnePlus: first scan added 253, second scan added 0/removed 0/changed 0.

Release evidence: versionCode 4/versionName 1.1.1, `test assembleDebug assembleAndroidTest` passed,
and FileProviderContractTest passed 1/1 on the designated OnePlus device.

## 1.2.0 — 2026-09-09

- Added target SDK and last update time to the read-only application detail.
- Added fact-based attention explanations for debuggable and disabled states without malware claims.
- Extended hashed repeat-scan comparison to include observed version, target SDK, update time, enabled state, and debug state.
- Preserved the existing Chinese UI hierarchy, no-permission posture, and non-destructive behavior.
