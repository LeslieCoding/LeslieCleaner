# CleanMyPC Next Installer Experience

## Modes

Offer Express and Custom installation. Custom mode selects path, shortcuts, startup, update channel and data retention. Validate disk space and remember the prior path without writing outside the selected boundary.

## Safety

Current-user and all-users modes are explicit. The installer provisions the integrity baseline only after the published files are present and aborts on a non-zero result. Shared data ACLs and uninstall data-preservation policy are recorded.

## Acceptance

Test silent/current-user/all-users install, UAC, upgrade backup, failed provisioning, launch, repair, uninstall-preserve and uninstall-delete paths.

## Implementation dossier

- IDs: `LCN-UX-INSTALLER-001..012`; installation declares scope, prerequisites, signing state, data location, and rollback behavior.
- Installer validates package hash/signature before execution and reports unsigned development packages visibly.
- Upgrade snapshots migration-critical data, executes migrations transactionally, and retains recovery diagnostics on failure.
- Uninstall defaults to preserving user data; deletion is a separate explicit choice with scope and backup warning.
- Logs record installer version, scope, result code, migration ID, integrity provisioning, and preserved-data decision.
- Acceptance uses a clean Windows matrix for current user, all users, elevation, repair, upgrade, downgrade refusal, and uninstall paths.
