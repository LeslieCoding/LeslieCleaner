# LeslieCleaner Android 3.0.0 — Test Strategy

## Unit

- Health threshold and unavailable-state rules.
- Application attention classification and normalized observed facts.
- Snapshot first/second/changed/migration behavior.
- Coordinator duplicate invocation, failure aggregation, and duration.
- Audit escaping, redaction, retention, and export content.
- Junk candidate confidence/category/action-route rules, including empty and inaccessible inputs.
- Security signal distinction between platform authority, local heuristic, and unavailable state.
- Startup receiver normalization, duplicate receiver handling, and settings intent fallback.
- Rule update version/signature/stale/failure/fallback behavior without network dependency.

## Android/instrumented

- FileProvider cache boundary and redacted export.
- Main activity launch and Compose semantics for scan/result/detail controls.
- Real PackageManager/health reader smoke on OnePlus PKG110/API 36.

## Regression

- Existing Chinese dashboard/navigation remains reachable.
- Scan action changes state and opens result dialog.
- Search/filter/detail/history/settings/export remain functional.
- No permissions or dangerous operations are introduced.
- Every module card opens a real result, explanation, or controlled system handoff; no empty click handler.

## Performance evidence

Record scan duration, item count, and result rendering boundary. Do not claim desktop Bible targets
for Android without an Android measurement. Unmeasured cross-device results remain NOT_VERIFIED.
