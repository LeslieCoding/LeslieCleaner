# V4 Security Integrity & Trusted Repair Report

- Module: `27_Security_Architecture`
- Scope: Core security contracts/models, Infrastructure security services and registrations, security tests
- Date: 2026-08-17 (Asia/Shanghai)
- Result: PASS

## Delivered outcome

V4 now has a fail-closed startup integrity and repair loop. It never turns arbitrary current files into a trusted baseline. A baseline can be created or replaced only through an explicitly authorized request with a non-empty trust reason. The sealed manifest covers all five architecture targets: executable, core library, configuration, database, and plugin metadata.

Verification writes an atomic `file-integrity-records-v4.json` snapshot containing stable IDs, paths, category, expected/current SHA-256, expected/current size, status, risk, last-check time, and description. The manifest itself is atomically written and locally sealed through the existing AES-GCM/DPAPI key ring. Optional detached ECDSA signatures are verified only against an externally configured trusted public key; a signature without such a key is rejected.

Low-risk configuration drift produces a warning and audit evidence without forcing emergency mode. Executable, core library, database, plugin metadata, unsafe path, reparse point, missing manifest, corrupt manifest, signature failure, and unreadable/hash failure are critical. Critical failures enter the existing emergency read-only gate and state explicit recovery conditions.

Repair verifies the trusted backup hash and optional signature before copying, restores through a same-directory temporary file and atomic replace, then re-verifies every manifest entry. Read-only mode is cleared only when:

1. every protected artifact passes re-verification;
2. database integrity passes;
3. audit-chain integrity passes;
4. permission policy is safe;
5. update trust passes;
6. plugin security passes;
7. encryption/key-ring health passes;
8. no repair error remains.

## Requirement mapping

| Architecture requirement | V4 evidence |
|---|---|
| Startup targets: executable/core/config/database/plugin metadata | `IntegrityArtifactKind`; baseline rejects missing categories |
| `FileIntegrityRecords` | Atomic `file-integrity-records-v4.json`; `FileIntegrityRecord` includes required and diagnostic fields |
| SHA-256 integrity | Streaming `SHA256.HashDataAsync`; fixed-time comparison |
| Optional signature verification | Detached ECDSA verification against externally trusted PEM key |
| First trusted baseline / explicit rebuild | `ExplicitlyAuthorized`, authorization reason, `ReplaceExisting`; missing baseline fails closed |
| Low-risk configuration handling | Notice-level record, warning audit, no emergency transition |
| High-risk core tampering | Critical record, emergency read-only, explicit repair conditions |
| Trusted repair -> restore -> recheck | Backup pre-verification, atomic restore, full post-verification |
| Conditional emergency recovery | Independent `IntegrityRecoveryPrerequisites`; gate clears only when all pass |
| Path security | Absolute root validation, boundary checks, traversal rejection, reparse-point rejection |
| Corrupt manifest | AES-GCM seal and JSON/schema validation; failure closed |
| Key rotation | Pre-decrypt all artifacts, rotate, atomic re-encrypt, retire old key only after success; rollback on failure/cancel |
| Health score | Exact boolean weights: integrity 30, permission 20, update 20, plugin 15, encryption 15 |
| Security audit | Baseline, integrity failure, repair success/failure events in hash-chained `SecurityAuditStore` |

## Files

Core:

- `src/LeslieCleaner.Core/Abstractions/IIntegrityRepairService.cs`
- `src/LeslieCleaner.Core/Models/IntegrityModels.cs`
- `src/LeslieCleaner.Core/Models/SecurityModels.cs`

Infrastructure:

- `src/LeslieCleaner.Infrastructure/Security/IntegrityRepairService.cs`
- `src/LeslieCleaner.Infrastructure/Security/IntegrityRepairService.Verification.cs`
- `src/LeslieCleaner.Infrastructure/Security/IntegrityRepairService.Repair.cs`
- `src/LeslieCleaner.Infrastructure/Security/IntegrityRepairService.Helpers.cs`
- `src/LeslieCleaner.Infrastructure/Security/ProtectedArtifactKeyRotationService.cs`
- `src/LeslieCleaner.Infrastructure/Security/SecurityManager.cs`
- `src/LeslieCleaner.Infrastructure/Security/SecurityPlatformRegistration.cs`
- `src/LeslieCleaner.Infrastructure/ServiceCollectionExtensions.cs`

Tests:

- `src/LeslieCleaner.Tests/Security/IntegrityRepairServiceTests.cs`
- `src/LeslieCleaner.Tests/Security/ProtectedArtifactKeyRotationServiceTests.cs`
- `src/LeslieCleaner.Tests/Security/SecurityManagerTests.cs`

No Data, AI, Update, Uninstaller, or Logging implementation file was modified by this module.

## Verification evidence

### Release build

Command target: `LeslieCleaner.V4.sln`, configuration `Release`, `--no-restore`.

Result: PASS, 0 warnings, 0 errors. All projects in the V4 solution compiled, including Core, Infrastructure, Data, modules, UI, App, Updater, plugin host, and test projects.

### Focused round 1

Result after correcting obsolete implicit-baseline expectations: 22 passed, 0 failed, 0 skipped, 392 ms.

TRX: `artifacts/test-results/v4-integrity-round1-rerun.trx`

The preserved initial run is `artifacts/test-results/v4-integrity-round1.trx`. It recorded 20 passed and 2 failed. Both failures were old tests that required the prohibited behavior “trust the current application file automatically on first assessment.” Production code was not weakened; the tests were changed to require fail-closed behavior. The rerun passed.

### Focused round 2

Result before the normal-install-root refinement: 25 passed, 0 failed, 0 skipped, 369 ms.

Final refinement rerun: 26 passed, 0 failed, 0 skipped, 653 ms.

TRX: `artifacts/test-results/v4-integrity-round2.trx`

Final TRX: `artifacts/test-results/v4-integrity-round2-final.trx`

Round 2 includes all round-1 security groups plus transactional key-rotation success, pre-validation failure preservation, cancellation preservation, and a real normal-install layout with executable/core under the application root, configuration under Config, database under Data, and plugin metadata under ProgramData.

## Tested failure paths

- missing baseline does not create one and enters read-only;
- baseline without explicit authorization is rejected;
- baseline missing any of the five required categories is rejected;
- path escape and symbolic-link/reparse targets are rejected;
- corrupt JSON/sealed manifest fails closed and is not replaced implicitly;
- low-risk configuration change is recorded without emergency escalation;
- high-risk core modification enters emergency mode;
- locked/unreadable core file becomes a critical hash-verification failure;
- trusted backup tampering prevents repair and keeps read-only mode;
- successful trusted restore is re-verified;
- incomplete audit recovery prerequisite prevents clearing read-only even after file repair;
- all recovery conditions clear read-only;
- cancellation is observed;
- detached signature is validated with an external trust key;
- key rotation preserves original files on invalid encrypted input or cancellation;
- successful rotation makes old-key ciphertext unreadable only after all artifacts are re-encrypted.

## Operational notes

The installer or another explicitly authorized provisioning flow must call `EstablishTrustedBaselineAsync` after authenticating the release payload. Normal startup must call the registered `IIntegrityRepairService` through `SecurityManager`; if the installer has not provisioned a trusted manifest, V4 deliberately starts fail-safe in read-only mode.

Old baseline documents are not automatically promoted. Re-establishment is intentionally explicit and produces security audit evidence.
## Normal-install explicit baseline handoff

The installer/provisioner must establish the baseline after it has verified the signed V4 package and initialized the first configuration/database/plugin catalog, but before launching the UI for the first time. Resolve `IIntegrityRepairService` from dependency injection and submit one `TrustedBaselineRequest`:

- `ApplicationRoot`: `AppContext.BaseDirectory` (normalized full path).
- `TrustedBackupRoot`: `IPathService.BackupDirectory` (outside the application root).
- `Executable`: `Environment.ProcessPath` or the installed LeslieCleaner executable.
- `CoreLibrary`: each shipped core/security library under `AppContext.BaseDirectory`.
- `Configuration`: the initialized `Path.Combine(IPathService.ConfigDirectory, "settings.json")`.
- `Database`: the initialized `Path.Combine(IPathService.DataDirectory, "LeslieCleaner.db")` after migrations and integrity verification.
- `PluginMetadata`: immutable installed `plugin.json` manifests; when none are installed, provision a signed empty plugin catalog under `Path.Combine(IPathService.ProgramDataRoot, "Plugins")` and include it.
- `ExplicitlyAuthorized`: `true` only in the verified installer/repair flow.
- `AuthorizationReason`: include the verified package/version/source.
- `ReplaceExisting`: `false` on first install; `true` only after an authenticated update or repair has reverified every target.

The normal UI must not silently establish a baseline from whatever files happen to exist. If installer provisioning is skipped, fail-safe read-only mode is intentional. Legitimate configuration/database/plugin mutations need an authorized lifecycle integration that validates the change and explicitly re-establishes the affected release baseline; a general startup fallback is forbidden.

## Final shared-build note

After the multi-root normal-install refinement, `LeslieCleaner.Infrastructure.csproj` built Release with 0 warnings/0 errors and `LeslieCleaner.Tests.csproj` built Release with project-reference rebuilding disabled, also 0 warnings/0 errors. The final 26-test security rerun passed. A simultaneous all-solution rebuild was blocked by an unrelated concurrent edit in `Modules.Startup/WindowsDelayedStartupTaskController.cs` (`CS8518`); this module did not modify that file. The earlier full-solution Release build, before that concurrent Startup edit, passed with 0 warnings/0 errors.