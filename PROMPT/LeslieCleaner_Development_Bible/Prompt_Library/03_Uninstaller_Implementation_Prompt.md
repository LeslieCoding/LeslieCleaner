# Smart Uninstaller Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent together with `Modules/23_Uninstaller.md`, `Modules/24_Residual_Engine.md`, and `00_Execution_Contract.md`.

## Mission

Implement or repair Smart Uninstaller for Windows 10/11. It discovers installed software from authoritative providers, launches only validated uninstall mechanisms, observes the result, and hands off to the Residual Engine. It does not claim that one universal uninstaller works for every application.

## Discovery sources

Enumerate and classify, with source retained on every record:

1. Win32 uninstall registry records in HKLM 32-bit, HKLM 64-bit, and HKCU views.
2. MSI ProductCode/UpgradeCode and installer-cache evidence.
3. Appx/MSIX/UWP and Microsoft Store package identity.
4. Winget provider metadata when available.
5. Game launchers (Steam, Epic, Battle.net) through their manifests/providers.
6. Adobe, Office, Visual Studio, Docker, WSL, Hyper-V, Node, Python, Java, and other environment-specific providers.

Deduplicate by stable provider identity before display. Display-name similarity may group the UI but must not merge different uninstall identities or scopes.

## Uninstall request validation

Inputs are an installed-app stable ID, source type, provider metadata, explicit confirmation, and cancellation token. Resolve to one typed provider request; do not pass raw registry command text to a shell.

Validate executable existence, allowed provider type, expected arguments, package identity, working directory, timeout policy, and elevation need. Redact credentials or secrets from logs and UI. Do not automatically terminate arbitrary same-name processes; surface a precise in-use/process decision and require a user-approved policy.

## Workflow

```text
Discover → Classify → Preview → Confirm → Authorize → Launch Provider
→ Observe Process/Provider Result → Verify Provider State → Create Residual Query
→ Run Residual Pipeline → Preview Residual Cleanup → History/Notification
```

Provider start is not success. Success requires provider-specific verification: registry/package state, exit code where meaningful, or an explicit provider completion signal. A non-zero exit code, timeout, cancellation, or unavailable provider produces a typed partial/failed state and blocks automatic residual cleanup unless the user starts a separate reviewed scan.

## Residual handoff

Pass product, publisher, install location, ProductCode, UpgradeCode, source type, provider evidence, and uninstall log location where available. The residual engine handles discovery; uninstaller workflow never recursively searches the disk itself.

Show residual candidates with source stage, confidence, risk, permission, backup/rollback availability, and why they were associated with the app. Low-confidence deep-scan candidates are unchecked by default.

## Logging and history

Emit discovery, provider selection, authorization, launch, PID/timeout, provider verification, residual handoff, cleanup result, and rollback events. History contains app stable ID, source, provider, exit code, elapsed time, outcome, residual operation ID, and recovery guidance.

## Tests

Cover Win32, MSI, Appx/Store, Winget unavailable, malformed command, missing executable, timeout, cancellation, non-zero exit, duplicate request, provider verification failure, in-use application, residual handoff, low-confidence candidate default, and rollback guidance.

Use fakes or isolated fixtures for provider execution. Real third-party uninstallers are manual Windows acceptance evidence, not ordinary automated tests.

## Handoff

Report provider coverage, source precedence, launch/verification behavior, residual integration, process policy, tests, benchmark data, and remaining manual provider matrix gaps.
