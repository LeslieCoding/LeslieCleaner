# Round 9: System-Operation Boundary Audit

## Findings and Fixes

| ID | Finding | Fix | Evidence |
|---|---|---|---|
| R9-001 | Maintenance task creation invoked `schtasks.exe` outside the integrity-failure gate. | `MaintenancePlanner.RegisterScheduledTaskAsync` now fails closed when the gate is not healthy. | `MaintenanceTaskRegistration_SecurityGateBlocked_ReturnsFalseWithoutLaunchingTaskScheduler` passed. |
| R9-002 | Toolbox service commands were a direct UI process launch path. | Service control now checks the gate for both individual and batch actions. | Security, AI-plan, and UI-service regression suite passed. |
| R9-003 | The individual service command accepted any `sc.exe` verb and did not require confirmation. | Only `start` and `stop` are accepted; each individual action requires an explicit confirmation. | Code-level execution boundary review; UI service remains protected by the existing core-service blacklist. |

## Verification

| Suite | Result |
|---|---|
| Security operation gate, AI planner, and UI services | 14 / 14 passed |
| Standard release regression (`Category!=LongRunning&Category!=Soak`) | 285 / 285 passed in 45 s |
| Self-contained installer install/uninstall | Passed; both waited processes returned exit code 0 |

## Delivery Artifact

- Installer: `installer/output/LeslieCleanerSetup-3.0.0.exe`
- SHA-256: `F3FC969F2CEA0E6A19BBFF99CE7E39A4DDB788B168D44D50392BBB42CA29B28A`
- Packaging: self-contained `win-x64`; a fresh temporary installation and complete uninstallation both succeeded.

## Boundary

Service control still relies on Windows SCM permissions and may correctly report a permission failure under a standard user account. Protected core services remain blocked before any command is issued.
