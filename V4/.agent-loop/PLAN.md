# LeslieCleaner Full Development Plan (V4+ Enhancement)

## Objective
Stabilize the existing V4 codebase by resolving all remaining bugs and then implementing a full suite of features as specified in the `PROMPT` directory architecture/design guides, adhering strictly to the project's "Development Bible".

---

## Phase 1: Stabilization & Bug Fixing (Foundation)

The immediate priority is to get a clean green test run for the existing V4 solution.

### Step 1.1: Fix Log Parsing & Diagnostics
- **Root Cause:** The regex in `LogSummaryBuilder.cs` fails to match standard log lines, causing error summaries and diagnostics reports to be empty or incomplete.
- **Action:**
    - Implement a robust, non-greedy regular expression in `LogSummaryBuilder.cs` that correctly captures:
        1. Timestamp (handle various layouts).
        2. Log Level (`[INF]`, `[WRN]`, `[ERR]`).
        3. Event Kind (`[ScanStart]`, etc.).
        4. Message content.
- **Verification:** Run `DiagnosticsServiceTests` and `UpdateDiagnosticsTests`.

### Step 1.2: Fix Plugin Crash Simulation Test
- **Root Cause:** `PluginRuntimeIntegrationTests` reports an `EXECUTION_TIMEOUT` when it expects a `HOST_CRASHED` status, suggesting the crash doesn't happen cleanly or is being caught/swallowed by the host implementation in a way that causes hang.
- **Action:** 
    - Investigate the plugin execution loop and failure handling logic in `LeslieCleaner.PluginHost`.
    - Ensure "fail-fast" behavior triggers the expected error status without causing a thread hang or timeout.

### Step 1.3: Fix Update E2E Race Condition
- **Root Cause:** `UpdateEndToEndTests` fails with "Temporary HTTP server not ready". This indicates a race condition where the test starts sending requests before the mock update server is fully listening.
- **Action:** 
    - Implement a proper retry/wait mechanism in `WaitForServerReady()` or ensure the server listener is explicitly awaited before the test proceeds.

### Step 1.4: Final V4 Sanity Check
- **Action:** Run a full solution build and all tests (`dotnet build` then `dotnet test`). No errors or failures should remain.

---

## Phase 2: Module Implementation (Driven by Development Bible)

Implementation will proceed following the "Module Independence" and "Layered Architecture" rules in `01_Project_Rules.md`. Every module will be a bounded context communicating via contracts/application layer.

### Step 2.1: Core Engine Enhancements
Implement or refine foundational engine components mentioned in architecture docs:
- **Startup Manager:** Implement scanning (Registry, Task Scheduler, Services) and classification logic according to `19_Startup_Manager_Architecture.md`.
- **Residual Engine:** Refine detection rules and quarantine/cleanup logic per `24_Residual_Engine.md`.

### Step 2.2: Feature Module Rollout
Each module will follow this cycle: **Requirement Review $\rightarrow$ Design (Contract) $\rightarrow$ Implementation ($\text{Core}+\text{Modules}$) $\rightarrow$ Persistence ($\text{Data/SQLite}$) $\rightarrow$ UI Integration $\rightarrow$ Testing.**

1.  **Registry & Uninstaller:** Implement professional-grade registry cleaning and smart uninstallation flows (`22_Registry_Cleaner.md`, `23_Uninstaller.md`).
2.  **Cleanup Modules:** Build out specific cleaners: Browser, Privacy Guard, File Shredder, Duplicate Finder, etc.
3.  **AI & Intelligence Layer:** Implement AI recommendation engines and smart scan logic based on the "AI Powered" mission.

### Step 2.3: UX/UI Overhaul (Premium Light Theme)
- **Design System Implementation:** Translate the design specifications (`10_UI_Design_System.md`, etc.) into WPF styles, templates, and resources.
- **Interaction Layer:** Implement high-quality animations and transition effects as per `12_Animation_Standard.md`.
- **Batch Operations & History Center:** Build out bulk management tools and historical activity tracking.

---

## Phase 3: Hardening & Release Preparation

### Step 3.1: Performance Optimization
- Apply profiling to every module. target measurable performance targets (CPU/Memory usage) as per the mission statement.
- Optimize database queries for all rule engines.

### Step 3.2: Final Validation
- Comprehensive regression testing of all implemented modules.
- Verification of Installer and Deployment logic (`14_Installer_And_Deployment_Specification.md`).
- Documentation update in `docs/`.

---

## Core Constraints & Rules (Reminder)
- **Architecture:** Strict Layered Architecture (UI $\rightarrow$ App $\rightarrow$ Domain $\rightarrow$ Infrastructure). No reverse dependencies.
- **Safety:** Always prioritize system stability and "Quarantine" over immediate deletion.
- **Rules Engine:** All logic must be rule-driven, not hardcoded.
- **Documentation:** Every public API MUST have XML documentation.
