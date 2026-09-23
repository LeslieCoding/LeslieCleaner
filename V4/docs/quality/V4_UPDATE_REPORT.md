# V4 Update System P1 Closure Report

Date: 2026-08-16  
Specification: `25_Update_And_Version_Manager_Architecture.md`  
Scope: `LeslieCleaner.Infrastructure/Updating`, `LeslieCleaner.Updater`, and update-focused tests.

## Delivered

- Channel-aware update discovery with `Stable`, `Beta`, and `Development`; Stable is the safe default and per-channel server URLs are supported.
- Production remote sources require credential-free HTTPS. Loopback HTTP remains available for offline/local update testing, and injected transports remain testable.
- ECDSA P-256 manifest verification remains default-on. The signed canonical payload now covers channel, compatibility, package choices, migration identities, restart requirement, file hashes, sizes, and URLs.
- SHA-256 file validation uses fixed-time hash comparison. Invalid metadata, absolute paths, traversal, cross-origin file URLs, and reparse-point paths are rejected.
- Full/incremental package resolver chooses a compatible incremental package for the exact source version when it is smaller; otherwise it selects the smallest full package. Legacy file-level manifests remain compatible.
- Compatibility preflight checks minimum current version, OS, process architecture, required application files, and safe dependency paths.
- Resumable downloads retain `.part` data after interruption, send HTTP `Range`, require a valid partial response, restart safely when the server returns a complete response, and enforce manifest byte size.
- Download control supports pause/resume through the shared pause token, cancellation, caller-defined timeout, aggregate byte progress, percentage, and measured bytes/second.
- Installation transaction hook backs up application database/configuration before compiled, application-owned migrations; it verifies migrated data and automatically restores the backup after migration, verification, or file-apply failure.
- The external Updater retains transactional staged replacement and partial-failure rollback. Source traversal now walks one directory level at a time and rejects every file/directory reparse point before following it.
- Atomic, bounded update history stores check/download/install/failure/rollback state. Each install transaction generates a JSON report with old/new versions, channel, package kind, changed files, migration state, rollback state, errors, and restart requirement.
- Existing public methods and legacy manifest constructors remain source-compatible.

## Security boundaries

- The manifest may identify a migration, but it cannot carry or execute an arbitrary script.
- File destinations must remain under the staging/install root.
- File download URLs must remain on the trusted update source origin.
- Unsigned manifests are rejected by default; test/offline opt-out remains explicit configuration.
- Failed content hash validation removes the untrusted staging version.
- User cancellation and network interruption preserve only the non-executable `.part` cache for resume.

## Verification evidence

Release build:

- Full solution `LeslieCleaner.V4.sln`: 0 warnings, 0 errors.
- Infrastructure-only and Updater-only Release builds: 0 warnings, 0 errors.

Focused update tests, independent runs:

- Round 1: 27 passed, 0 failed, 0 skipped.
  - `artifacts/test-results/v4-update-platform-final.trx`
- Round 2: 27 passed, 0 failed, 0 skipped.
  - `artifacts/test-results/v4-update-platform-final-round2.trx`

Covered cases include:

- semantic version comparison and no-update behavior;
- Stable/Beta/Development channel matching;
- full versus incremental package selection;
- OS/architecture/dependency rejection;
- successful check/download/apply/restart end-to-end flow;
- Range resume from an existing partial file;
- cancellation propagation;
- bad hash cleanup;
- missing/invalid ECDSA signature rejection;
- manifest and destination path traversal rejection;
- cross-origin package URL rejection;
- mid-replacement file rollback;
- migration failure data restore;
- update history and transaction report persistence;
- diagnostics local fallback and upload behavior.

## Failure history retained

The first post-change focused run exposed four real regressions: three file moves occurred before the asynchronous output stream was released, and diagnostics success no longer emitted the expected event. Both were fixed. A later 26/27 run had one environment-only failure because the temporary Python HTTP server was not on `PATH`; rerunning with the workspace-provided Python runtime produced the two clean 27/27 results above. The failed TRX was not overwritten.

## Remaining integration note

The application UI still decides when to call the install transaction and external Updater. The update core enforces the secure transaction and exposes the required status/report data, but user-facing scheduling and notification presentation belong to the later UI/installer pass.