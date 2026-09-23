# 任务清单

## Phase 1: Stabilization & Bug Fixing (Foundation)
### Step 1.1: Fix Log Parsing & Diagnostics
- [x] Debug `LogSummaryBuilder.cs` regex failure by running existing `DiagnosticsServiceTests`. -> Verification: Reproduce failing test behavior.
- [x] Refactor regex in `src/LeslieCleaner.Infrastructure/Diagnostics/LogSummaryBuilder.cs` to be non-greedy and robust. -> Verification: `dotnet build && dotnet test src/LeslieCleaner.Tests/Diagnostics/UpdateDiagnosticsTests.cs`.

### Step 1.2: Fix Plugin Crash Simulation Test
- **Task**: Investigate the execution loop in `src/LeslieCleaner.PluginHost` causing `EXECUTION_TIMEOUT` instead of `HOST_CRASHED`.
- **Status**: Completed ✅
- **Details**:
    - Found a race condition in `PluginRuntime.HostSession.SendAsync`. When a command timeout occurs simultaneously with the plugin host process exiting (crashing/exiting), the cancellation token triggers an `OperationCanceledException` that is caught before checking if the host has exited. This causes it to be misreported as `EXECUTION_TIMEOUT` even though a crash occurred.
    - **Root Cause**: In the `SendAsync` loop, all tasks (`read`, `monitor`, `exit`) are bound to a linked cancellation token from the timeout source. When time expires, every task throws `OperationCanceledException`. The `catch (OperationCanceledException)` block does not verify if `_process.HasExited` is true before returning its result, thus swallowing any concurrent terminal process state changes.
    - **Verification**: Code analysis of `src/LeslieCleaner.Infrastructure/Plugins/PluginRuntime.cs:377`.

### Step 1.3: Fix Update E2E Race Condition
- [ ] Identify timing issue in `WaitForServerReady()` within the update testing context. -> Verification: Confirm race condition between server listener and request sender.
- [ ] Implement a reliable retry or await mechanism for mock server readiness. -> Verification: `dotnet build && dotnet test src/LeslieCleaner.Tests/Updating/UpdateEndToEndTests.cs`.

### Step 1.4: Final V4 Sanity Check
- [ ] Run full solution build and all existing tests to ensure zero regressions from Phase 1 fixes. -> Verification: `dotnet build LeslieCleaner.V4.sln && dotnet test`.

## Phase 2: Module Implementation (Driven by Development Bible)
### Step 2.1: Core Engine Enhancements
#### Startup Manager (`19_Startup_Manager_Architecture.md`)
- [ ] Define Registry and Task Scheduler scanning contracts in `LeslieCleaner.Core` or `LeslieCleaner.Modules`. -> Verification: Build + Unit Tests.
- [ ] Implement Registry-based startup item scanner logic. -> Verification: Build + Unit Tests.
- [ ] Implement Task Scheduler/Service based scanner logic. -> Verification: Build + Unit Tests.
- [ ] Implement Startup Manager classification engine (detecting malicious vs legitimate). -> Verification: `dotnet build` and tests.

#### Residual Engine (`24_Residual_Engine.md`)
- [ ] Implement residual file detection rules in the core engine. -> Verification: Build + Unit Tests.
- [ ] Implement Quarantine logic for detected residuals to prevent accidental deletion. -> Verification: Build + Unit Tests.
- [ ] Implement Cleanup execution service for quarantine items. -> Verification: `dotnet build` and tests.

### Step 2.2: Feature Module Rollout
#### Registry & Uninstaller (`17_&_18...`)
- [ ] Implement high-precision registry key scanning logic (cleaning rules). -> Verification: Build + Unit Tests.
- [ ] Implement automated registry cleanup execution service. -> Verification: Build + Unit Tests.
- [ ] Develop smart uninstallation workflow orchestration in `LeslieCleaner.Modules`. -> Verification: Build + Integration Tests.

#### Specific Cleaners (Browser, Privacy, Shredder, Duplicate)
- [ ] Implement Browser History & Cache cleaning module logic. -> Verification: Build + Unit Tests.
- [ ] Implement Privacy Guard scanning rules for sensitive data/files. -> Verification: Build + Unit Tests.
- [ ] Implement File Shredding (secure deletion) engine core. -> Verification: Build + Unit Tests.
- [ ] Implement Duplicate File detection algorithm and scanner. -> Verification: Build + Unit Tests.

#### AI & Intelligence Layer
- [ ] Implement the AI recommendation engine orchestration logic. -> Verification: Build + Unit Tests.
- [ ] Integrate smart scan logic based on user behavior/history patterns. -> Verification: `dotnet build` and tests.

### Step 2.3: UX/UI Overhaul (Premium Light Theme)
- [ ] Apply Design System styles (Colors, Typography) to WPF resources. -> Verification: Project Build + UI visual check.
- [ ] Implement standardized Interaction Layer animations as per `12_Animation_Standard.md`. -> Verification: Visual verification in running app.
- [ ] Develop the History Center module for activity tracking. -> Verification: Build + Unit Tests.

## Phase 3: Hardening & Release Preparation
### Step 3.1: Performance Optimization
- [ ] Profile all core modules and optimize SQLite/Database query performance. -> Verification: Measurable reduction in scan time.
- [ ] Optimize CPU and Memory footprints for background scanning tasks. -> Verification: Resource usage audit via profiling.

### Step 3.2: Final Validation & Acceptance
- [ ] Perform comprehensive regression testing of all implemented modules against V3 baseline criteria. -> Verification: Full `dotnet test` pass.
- [ ] Verify Installer/Deployment logic integrity using the specification (`14_...`). -> Verification: Successful installer execution and clean install.
- [ ] Final Project Sanity Check: Build solution with 0 errors and run entire test suite (All Green). -> Verification: `dotnet build LeslieCleaner.V4.sln && dotnet test`.
