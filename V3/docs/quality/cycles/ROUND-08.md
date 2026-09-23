# Round 8: Delivery-Security Re-Audit

## Findings and Fixes

| ID | Finding | Fix | Evidence |
|---|---|---|---|
| R8-001 | Production settings were written as plaintext JSON although the architecture requires encrypted local configuration. | `JsonConfigService` now stores an authenticated AES-GCM envelope through the DPAPI-protected key service. Existing plaintext settings are read once and migrate on the next write; malformed or tampered envelopes fail closed to defaults and are logged. | `EncryptedConfigServiceTests` covers encrypted persistence, cross-instance reload, tamper rejection, plaintext migration, and actual DI registration. |
| R8-002 | `UninstallExecutor` could start an external uninstaller after an integrity failure. | Both batch and individual uninstall entry points enforce `ISecurityOperationGate` before process launch. | `UninstallSecurityGateTests` verifies that no command is launched when the gate is closed. |
| R8-003 | Update manifest file paths and staging-version paths were not constrained to their expected roots. | Update checking and downloading now reject traversal, rooted paths, invalid version directories, negative sizes, and invalid SHA-256 metadata before any file operation. | `UpdateServiceTests` covers check-time and download-time traversal rejection; update end-to-end flow still passes. |

## Verification

| Suite | Result |
|---|---|
| Configuration encryption, updater, and uninstall gate | 17 / 17 passed |
| Update check/download/hash/apply end-to-end | 17 / 17 passed |
| Standard release regression (`Category!=LongRunning&Category!=Soak`) | 284 / 284 passed in 44 s |
| Long-running performance suite | 16 / 16 passed in 2 m 44 s |
| Soak suite | 1 / 1 passed; process wall time 10 m 25 s |
| Self-contained installer install/uninstall | Passed; both waited processes returned exit code 0 |
| Published WPF application startup | Passed; main window created after 8 s in a fresh process |

## Delivery Artifact

- Installer: `installer/output/LeslieCleanerSetup-3.0.0.exe`
- SHA-256: `D5390482839F52ABC33EEE0D2D25376ECB9B4833015ECBC8B7D691BD03DFE550`
- Packaging: self-contained `win-x64`; installation and uninstallation were verified in a fresh temporary directory.

## Scope Boundary

The update client now has strict path and hash metadata validation. A production update channel still requires a server-side signing-key provisioning and code-signing process before it can be represented as an independently signed supply chain.
