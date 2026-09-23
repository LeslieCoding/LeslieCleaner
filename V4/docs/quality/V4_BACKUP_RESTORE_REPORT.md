# V4 Backup and Restore Module Report

Date: 2026-08-13
Specification: `PROMPT/Cleaner/LeslieCleaner_Development_Bible/Main/22_Backup_And_Restore_System.md`

## Outcome

The V4 backup service was extended in place without removing the legacy registry API or changing the legacy `Backups/Registry/{id}` layout. Dependency injection was already wired through `AddLeslieCleanerInfrastructure` as `IBackupService -> RegistryBackupService` and remains compatible.

## Implemented

- Unified storage index at `Backups/index.json`, rebuilt from authoritative per-backup metadata when listing.
- Typed records for Registry, File, Configuration, and Database backups.
- File strategies: metadata-only, streaming full copy, and move-after-verified-copy.
- Stable list and lookup-by-ID operations.
- Restore by ID with explicit Fail, Overwrite, and Rename conflict policies.
- SHA-256, size, existence, and readability verification.
- Restore rejects missing, unreadable, size-mismatched, or hash-mismatched payloads before changing the target.
- Overwrite restore first creates a current-state backup; registry restore also snapshots accessible current state before import/write.
- Atomic write-through replacement for both per-record metadata and the unified index; temporary files are removed in `finally` blocks.
- Manual deletion, lock/unlock protection, and retention by maximum age, maximum count, and optional storage size.
- Retention never automatically removes the newest backup and never removes locked backups.
- Audit events for create, verify success/failure, restore start/completion/failure, corrupt restore rejection, lock/unlock, delete, and retention cleanup.
- Legacy registry records without SHA-256 remain readable and are checked by existence, readability, and size for backward compatibility.
- Metadata payload paths are constrained to the managed backup root before being accepted into the index.

## Changed files

- `src/LeslieCleaner.Core/Abstractions/IBackupService.cs`
- `src/LeslieCleaner.Core/Models/BackupRecord.cs`
- `src/LeslieCleaner.Infrastructure/Services/RegistryBackupService.cs`
- `src/LeslieCleaner.Tests/BackupRestoreServiceTests.cs`
- `docs/quality/V4_BACKUP_RESTORE_REPORT.md`

No Plugin, Updater, or Diagnostics source was modified by this module task.

## Verification evidence

- Core Release build: passed, 0 warnings, 0 errors.
- Infrastructure Release build: passed, 0 warnings, 0 errors.
- The repository's local SDK required `GenerateTargetFrameworkAttribute=false` because the current generated framework-attribute cache reports MSB3992 without that property.
- Focused test command reached test-project compilation but did not execute tests because `Plugins/PluginRuntimeIntegrationTests.cs:87` currently resolves `SamplePlugin` as a namespace instead of a type. This file belongs to another parallel module and was not modified here.
- One analyzer issue in the new backup test (`xUnit2031`) was corrected after that attempt.

## Test inventory added

Seven focused tests cover:

1. typed file/config backup, index, lookup, list, and verification;
2. corrupt payload rejection and failure audit;
3. current-state snapshot before overwrite restore;
4. atomic metadata/index JSON with no orphan temporary files;
5. count retention with locked/newest preservation;
6. locked delete rejection followed by successful unlocked deletion;
7. metadata-only verification and restore refusal.

Executed passing test count in the final attempt: **0**. The test runner never started because the shared test assembly had the unrelated compile error above.

## Remaining verification

- Re-run the seven focused tests after the shared Plugin test compilation issue is fixed.
- Run the existing `RegistryBackupPathTests` and `RegistryEngineTests` compatibility suites.
- Run a full solution build and both requested full functional test rounds.
- Add/execute explicit age-retention and storage-ceiling tests if stronger branch-level evidence is required.
- Exercise registry export/import integration on an elevated Windows test environment.

## Risk assessment

Implementation compilation is clean at Core and Infrastructure scope. Functional test evidence is incomplete because no test case executed in the final run; therefore this report does not claim end-to-end acceptance of the backup module yet.
