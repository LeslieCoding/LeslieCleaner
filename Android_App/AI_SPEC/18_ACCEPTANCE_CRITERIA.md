# LeslieCleaner Android 3.0.0 — Acceptance Criteria

| ID | Criterion | Evidence required | Status |
|---|---|---|---|
| AC-001 | REG pipeline is complete and READY/READY_WITH_LIMITATIONS before code changes. | REG_OUTPUT, ENGINEERING_BASELINE, ASSUMPTION_REGISTER, REQUIREMENT_TRACEABILITY | PASS |
| AC-002 | APK version is 2.0.0 with higher versionCode. | dumpsys package output | PASS |
| AC-003 | Homepage analysis starts from the current scan action and displays a real result boundary. | OnePlus UI dump and audit record | PASS |
| AC-004 | Result includes application classifications and device health facts. | OnePlus UI dump plus unit tests | PASS |
| AC-005 | Repeat analysis reports first/second delta counts and stores no raw package identity. | snapshot tests and OnePlus audit evidence | PASS |
| AC-006 | Application detail explains public facts without malware conclusions. | semantics/UI evidence and attention rule tests | PASS |
| AC-007 | Failure states are Chinese, bounded, audited, and do not crash the activity. | typed failure path and audit contract | PASS_WITH_LIMITATION |
| AC-008 | No dangerous permission, Root, hidden API, silent deletion, or external device operation is introduced. | manifest/static audit and device policy record | PASS |
| AC-009 | Gradle unit tests, debug APK, androidTest APK, and FileProvider contract pass. | build and instrumentation logs | PASS |
| AC-010 | LAEP project state, progress, decisions, issues, changelog, matrix, and evidence are synchronized. | final audit and trace output | PASS |
| AC-011 | Junk management shows safe candidates/categories and a system-mediated cleanup route. | OnePlus UI + unit tests | PENDING |
| AC-012 | Security center shows platform/local/unavailable boundaries and Play Protect handoff. | OnePlus UI + signal tests | PENDING |
| AC-013 | Startup center shows observable receiver signals and explains control limitations. | OnePlus UI + reader tests | PENDING |
| AC-014 | Acceleration combines memory/storage pressure with safe next actions. | OnePlus UI + health tests | PENDING |
| AC-015 | Cloud rule update is offline-first, signed/versioned, and does not contact an unapproved endpoint. | update contract tests | PENDING |
| AC-016 | All module cards are functional and no longer route only to app search. | Compose semantics + OnePlus smoke | PENDING |
