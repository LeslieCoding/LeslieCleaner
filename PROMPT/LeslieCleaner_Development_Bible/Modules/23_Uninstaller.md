# CleanMyPC Next Uninstaller Module

## Objective

Uninstall Windows software and locate owned residue quickly, transparently and reversibly.

## Discovery order

Use installed application databases, MSI/Appx/Store/Winget metadata, uninstall strings, installation logs, publisher, product code, GUID and signed known rules before any deep scan. Never start with full-disk recursion.

## Residual transaction

Capture an ownership fingerprint, run the official uninstaller when available, rescan staged evidence, classify residuals, back up and quarantine only high-confidence matches. Services, tasks, drivers and startup items require separate evidence and confirmation.

## Acceptance

Test Win32, MSI, Appx, Store, Winget, game, Adobe, Office, Visual Studio, Docker, Node, Python, Java and browser-extension fixtures, including failed uninstall, locked residue and rollback.

## Implementation dossier

- IDs: `LCN-UNINSTALL-WORKFLOW-001..014`; input is an installed-app identity plus provider and residual policy.
- Pipeline: discover → classify → preview → select provider → authorize → launch → monitor → verify → residual handoff.
- Provider commands are parsed into typed executable/arguments; arbitrary shell text is forbidden.
- Output includes source, exit code, timeout, process ID, residual query, and recovery guidance.
- Logs never store secrets or unredacted command credentials.
- Tests cover malformed strings, missing executables, MSI/Appx fallback, timeout, cancellation, duplicate invocation, and residual failure.
