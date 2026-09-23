# Round 10: Published-Process Performance Tuning

## Measurement Method

Each sample starts the self-contained `win-x64` executable, polls for a non-zero main-window handle, waits for idle, records process working set/private memory, then terminates only the process created by the measurement. The machine-specific results are not generalized to other hardware.

## Baseline and Decision

| Publish mode | Cold window ready | Warm window ready | Idle working set | Private memory | Decision |
|---|---:|---:|---:|---:|---|
| ReadyToRun | 617 ms | 504 ms | 230.1 MB | 169.0 MB | Rejected: idle memory exceeds the Bible limit. |
| No ReadyToRun | 4,367 ms | 394 ms | 88.3 MB | 25.1 MB | Selected: memory passes and cold start remains within the documented 5 s acceptable upper bound. |
| Composite ReadyToRun | 6,441 ms | 362 ms | 88.1 MB | 34.8 MB | Rejected: cold start exceeds the upper bound. |

## Change

`LeslieCleaner.App.csproj` disables `PublishReadyToRun`. This trades some first-process JIT time for a substantial reduction in idle resident memory; normal warm launches retain sub-second window readiness.

## Final Verification

- Corrected an output-directory contamination issue: stale ReadyToRun files remained after a publish-property change. Final package builds now clear only the verified `installer/publish` output directory before publishing.
- Fixed `MainWindow.BuildNavigationItems` to retain `ViewModelFactory` delegates instead of resolving every page ViewModel while the main window is constructed.
- Migrated the integrity baseline from a single installation path to a compatible multi-installation manifest. A new installation path can establish its first baseline; a later modification at that same path remains rejected. Security regression tests cover this migration.
- Standard regression: 286 passed, 0 failed (`Category!=LongRunning&Category!=Soak`).
- Installer smoke: silent install exit code 0, expected application and uninstaller files present, silent uninstall exit code 0, and the temporary installation directory removed.
- Final installer: `installer/output/LeslieCleanerSetup-3.0.0.exe`, 55,976,631 bytes, SHA-256 `978418D1878194D733C1C6931BD53B2D4A41F290A24EBF1CDA151C1FC07C7EF0`.

## Final Process Measurement

The final process reached a titled `LeslieCleaner` main window in 779 ms cold and 612 ms warm. At five seconds after window readiness, working set was 233.6–233.9 MB and private memory was 163.9–164.1 MB on this machine. Startup targets pass; the 120 MB idle working-set target does not yet pass under this full-security startup configuration, so this result is recorded as an open performance limitation rather than a release claim.

## Diagnostic Follow-up

- `dotnet-gcdump` measured a 4.1 MB managed heap after idle; the working-set excess is not a managed-object retention leak.
- Process-module sampling identified WPF/Intel graphics driver mappings as the dominant resident images. Disabling Mica did not prevent those mappings. A software-rendering experiment reduced private memory to about 80 MB, but left a 158 MB working set and pushed cold startup beyond the documented five-second upper bound; it was therefore reverted and is not shipped.
- The current package retains the low-risk Mica opt-in default, encrypted configuration, operation gates, independent install baselines, and navigation factory correction.
- Final regression after the retained changes: 286 passed, 0 failed. Final silent install/uninstall smoke: both exit code 0 and temporary installation directory removed.
- Latest installer SHA-256: `32876574F6AB6C2A1D30B61D15C31B019B2DCD543D43AD4AA4CF2D230F6CE19A`.

## Published UI Smoke

All 13 navigation destinations (`首页`, `Smart Scan`, `注册表清理`, `智能卸载`, `启动项管理`, `隐私保护`, `文件粉碎`, `重复文件查找`, `大文件分析`, `工具箱`, `平台中心`, `运行日志`, and `设置`) were launched individually through the published executable's `--page` hook. Every run reached a live main window without a process exit. Observed working sets ranged from 233.7 MB on the dashboard to 346.1 MB on the startup-manager page; these are per-page evidence samples, not cross-device guarantees, and identify the startup-manager, privacy, and logs views as priority memory-profiling targets.

## Startup Verification Split

- The startup-manager page now performs a truthful low-cost initial enumeration. File size and source classification are available immediately; signature verification and executable SHA-256 collection are exposed through the explicit `深度验证` action.
- A new signature-verification state distinguishes `NotChecked` from a verified invalid/unsigned result so the UI does not represent deferred work as a failed signature.
- Startup-specific regression: 43 passed, 0 failed. Full standard regression: 286 passed, 0 failed.
- The initial-page measurement did not reduce this machine's startup-manager private memory, which remains dominated by Windows task/service enumeration. The split is retained for correctness, cancellation-friendly user control, and to avoid unnecessary file signature work on page entry.
- Current installer SHA-256: `DA7268A8574775E824FD4B64F98C374A3B3FFEB08E6D867460F3597561111C9A`.
- Final silent installer smoke for this exact hash: install exit code 0, application and uninstaller files present, uninstall exit code 0, and temporary installation directory removed.

## Signed Update Manifest Gate

- Update manifests now contain optional `signature` and `signingKeyId` fields. The signed payload is a deterministic UTF-8 JSON projection of version, ordered file metadata, release notes, and key identifier.
- When an update source is configured, `update:requireSignedManifest` defaults to `true`. Missing signatures, missing public keys, malformed Base64, malformed keys, and invalid ECDSA P-256/SHA-256 signatures are rejected before file comparison, download, or staging. Offline mode remains unchanged when no update source is configured.
- A temporary compatibility mode (`update:requireSignedManifest=false`) exists solely for controlled legacy/self-hosted test feeds; it must be explicitly configured and never becomes the production default.
- Update signature/path/hash/end-to-end regression: 20 passed, 0 failed. Full standard regression: 288 passed, 0 failed. Latest silent install/uninstall smoke: both exit code 0 and temporary installation directory removed.
- Current installer SHA-256: `174747D683FA5CDD2B7BED48ECB6243B5F2CACA5862DBDACDCD812DD0D03545A`.
