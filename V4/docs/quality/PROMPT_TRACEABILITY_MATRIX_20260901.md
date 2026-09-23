# CleanMyPC Next Prompt Traceability Matrix

This matrix binds the Development Bible to implementation evidence. A prompt file is not considered implemented merely because it contains text; each requirement must point to source code, automated tests, and an acceptance boundary.

## Evidence status

- **Implemented**: source and automated evidence exist for the documented scope.
- **Partial**: a working slice exists, but one or more documented capabilities remain incomplete.
- **PendingEvidence**: implementation may exist, but the required test, benchmark, or real Windows evidence is missing.
- **NotImplemented**: no production implementation is claimed.

## Category mapping

| Bible files | Primary implementation | Automated evidence | Current status |
|---|---|---|---|
| `00_Master_System_Prompt.md`, `01_Project_Rules.md`, `02_Architecture.md`, `03_Project_Structure.md`, `04_Coding_Standard.md` | `src/LeslieCleaner.*/*.csproj`, `Directory.Build.props`, project boundaries | Release solution build; architecture/DI smoke tests | Partial: some rules remain governance-only |
| `05_Performance_Standard.md`, `55_Benchmark.md` | `src/LeslieCleaner.Infrastructure/Diagnostics`, `src/LeslieCleaner.Tests/EngineBenchmarkTests.cs`, `UninstallerBenchmarkTests.cs` | Performance integration and benchmark tests | Partial: machine-specific cold-start baselines pending |
| `06_UI_Design_System.md`, `10_UI_Design_System.md`–`17_Interaction_Design.md` | `src/LeslieCleaner.UI/Themes`, `Views`, `Controls`, `ViewModels` | `src/LeslieCleaner.Tests/UI` | Partial: accessibility and visual matrix need a signed-off run |
| `07_Module_Architecture.md`, `20_Smart_Scan.md`, `21_Cleaner.md`, `Prompt_Library/05_Cache_Rules_Implementation_Prompt.md` | `src/LeslieCleaner.Modules/Cleaner`, `Scanning/Rules/smart-scan-rules.json`, `CacheRuleCatalog.cs`, `SmartScanEngine.cs`, `src/LeslieCleaner.UI/ViewModels/SmartScanViewModel.cs` | `CacheRuleCatalogTests.cs`, `SmartScanEngineTests.cs`, Cleaner, integration, and flow tests | Partial: embedded versioned Rule Pack validates IDs, provider/version metadata, safe paths, and duplicate IDs; signed external rule updates and complete provider coverage remain pending |
| `22_Registry_Cleaner.md` | `src/LeslieCleaner.Infrastructure/Registry`, `src/LeslieCleaner.Modules/Cleaner/CleanEngine.cs`, `src/LeslieCleaner.UI/ViewModels/RegistryViewModel.cs`, `RegistryElevationResumeStore.cs` | Registry rules/physical-view fingerprint, exact-scope backup integrity, permission/UAC resume, typed-target collision, deletion readback, and rollback tests | Implemented for current scope: physical 32/64-bit views, conservative command resolution, unambiguous key/value identity, verified backup and readback are hard gates; one-time UAC state only restores selection after a fresh scan and never auto-deletes; protected/WRP items still require the privileged Windows matrix |
| `23_Uninstaller.md`, `24_Residual_Engine.md` | `src/LeslieCleaner.Modules/Uninstaller/Workflow`, `Residual`, `Discovery`, `Execution` | `ResidualPipelineTests.cs`, `UninstallWorkflowTests.cs`, `ResidualMatchScorerTests.cs`, parser tests | Partial: residual directories use cancellable, reparse-safe traversal with quarantine/rollback verification; residual rules and platform providers continue to expand |
| `25_Startup_Manager.md` | `src/LeslieCleaner.Modules/Startup`, `src/LeslieCleaner.UI/Services/StartupResumeTarget.cs` | Startup lifecycle/platform tests, `StartupResumeTargetTests.cs`, UI acceptance tests | Partial: UAC restart restores the validated target after real enumeration; deep verification/long-term telemetry pending |
| `26_Privacy_Guard.md`, `27_Browser_Cleaner.md`, `Prompt_Library/07_Privacy_Browser_Implementation_Prompt.md` | `src/LeslieCleaner.Modules/Privacy`, `src/LeslieCleaner.Modules/Browser/BrowserCleanerEngine.cs`, browser rule providers | `BrowserCleanerTests.cs`, privacy and rollback tests | Partial: Firefox profile traversal/reparse points are denied and cache recursion is cancellation-aware; browser/provider coverage is finite and explicit |
| `28_File_Shredder.md`, `29_Duplicate_Finder.md`, `30_Large_File_Analyzer.md` | `src/LeslieCleaner.Modules/Shredder`, `Duplicates`, `LargeFiles` | module and integration tests | Partial: large-scale and visual evidence pending |
| `31_Service_Manager.md`, `32_Driver_Manager.md`, `33_Task_Scheduler.md`, `34_Extension_Manager.md` | `src/LeslieCleaner.Modules/Startup`, `Platform`, related UI services | targeted platform tests where available | Partial/PendingEvidence; privileged Windows matrix required |
| `35_AI_Assistant.md`, `50_AI_Rules.md`–`56_Development_Workflow.md` | `src/LeslieCleaner.Infrastructure/AI`, `src/LeslieCleaner.UI/ViewModels` | AI assistant, redaction, safety, and history tests | Implemented for local/current P1 scope; provider quality is not guaranteed |
| `36_Settings.md`, `40_Installer.md`, `41_Batch_Operation.md` | `src/LeslieCleaner.UI/ViewModels/SettingsViewModel.cs`, installer scripts, batch services | UI acceptance tests and real current-user installer smoke | Partial: all-users/UAC/signing matrix pending |
| `42_History_Center.md`, `43_Notification_Center.md`, `44_Search_System.md`, `45_Favorites.md` | `src/LeslieCleaner.UI/Services`, `ViewModels`, `Infrastructure/Services` | UX service tests, history and search tests | Implemented for current scope |
| `46_Right_Click_Menu.md`, `47_Undo_System.md`, `48_AI_Recommendation.md` | context menu/undo services, clean rollback, AI recommendation surfaces | rollback, batch, UI, and AI tests | Partial: full shell integration and recommendation calibration pending |
| `51_Code_Review.md`, `52_Testing.md`, `53_Security.md`, `54_Logging.md` | `src/LeslieCleaner.Infrastructure/Logging`, `Diagnostics`, `Security`; `src/LeslieCleaner.UI/ViewModels/LogsViewModel.cs`; test projects | Latest broad run 444/444 excluding long-running Benchmark/Integration; focused durable logging/registry/UI run 33/33; plugin integration 4/4 | Implemented for tested scope: synchronous structured audit JSONL complements bounded async logs, self-log captures sink faults, and live viewer/diagnostics include both formats; long-running performance matrix remains machine-dependent |

## Mandatory change trace

Every future feature or bug fix must add all of the following in the same change:

1. A requirement ID in the relevant Bible file.
2. A source path and permission boundary in this matrix.
3. At least one deterministic automated test.
4. A structured log event for start, decision, result, and failure.
5. A rollback or quarantine path for destructive operations.
6. A Windows acceptance command or explicit `PendingEvidence` note.

## Prompt Library mapping

| Detailed prompt | Required implementation evidence |
|---|---|
| `Prompt_Library/00_Development_Agent_System_Prompt.md` | All changes: requirement ID, source/test mapping, permission boundary, rollback, logs, and truthful evidence status. |
| `Prompt_Library/01_Residual_Engine_Implementation_Prompt.md` | `ResidualScannerEngine`, `ResidualRuleImporter`, cleanup workflow, residual pipeline tests, and bounded-stage benchmarks. |
| `Prompt_Library/02_Registry_Engine_Implementation_Prompt.md` | Registry scanner/engine, backup service, physical-view/read-back tests, and privileged Windows acceptance. |
| `Prompt_Library/03_Uninstaller_Implementation_Prompt.md` | Installed-app discovery, uninstall workflow/executor, provider tests, residual handoff, and manual provider matrix. |
| `Prompt_Library/04_Cleaner_Engine_Implementation_Prompt.md` | Clean engine, quarantine/backup services, rollback/read-back tests, and batch UX evidence. |
| `Prompt_Library/05_Cache_Rules_Implementation_Prompt.md` | Smart Scan, browser/cache providers, rule data/versioning, direct-location tests, and rule-stage benchmarks. |
| `Prompt_Library/06_Startup_Manager_Implementation_Prompt.md` | Startup providers, impact scoring, reversible changes, protected policies, and privileged acceptance. |
| `Prompt_Library/07_Privacy_Browser_Implementation_Prompt.md` | Privacy/browser modules, profile/lock policies, session protection, rollback tests, and browser matrix evidence. |
| `Prompt_Library/08_AI_Assistant_Implementation_Prompt.md` | AI module, redaction/typed response validation, safety tests, and provider/offline evidence. |

## Current priority queue

1. Residual Engine: expand rule identity with product code, GUID, uninstall log, and publisher evidence while keeping bounded scans.
2. Cache Rules: add provider-specific cache rules for Windows 11 browsers, developer tools, games, and AI clients.
3. Registry Rule Engine: keep physical 32/64-bit views, read-back verification, and protected-path policy as hard gates.
4. Privileged acceptance: exercise all-users install, UAC elevation, rollback, and signed package verification on a clean Windows 11 image.
5. UX completion: finish accessibility, visual regression, and large-list virtualization evidence.
