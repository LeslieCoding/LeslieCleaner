# CleanMyPC Next Driver Manager

## Objective

Identify obsolete or orphaned drivers without destabilizing Windows.

## Discovery

Use Plug and Play inventory, driver store metadata, signer, provider, version, device association and active usage. Never infer ownership from a filename alone.

## Operations

Removal is disabled for boot, storage, display, network and security-critical devices by default. A
confirmed removal requires elevation, exports the exact published package, verifies per-file size
and SHA-256 metadata, and performs a post-delete package-store read-back. `/force` removal is not
permitted. If read-back fails, the verified package backup is used for a restore attempt. Automatic
restore-point creation and reboot orchestration remain explicit host-acceptance boundaries.

## Acceptance

Test signed/unsigned drivers, active device dependencies, missing package, access denied, cancellation and post-reboot verification boundary.

## Implementation dossier

- IDs: `LCN-DRIVER-MANAGER-001..010`; inputs are driver package, device association, signer evidence, version, and rollback request.
- Output includes package identity, provider, signer, affected devices, reboot requirement, and rollback state.
- Driver changes require elevation. Unsigned, boot-critical, or policy-protected packages are refused by default.
- Missing package, active device, invalid signature, dependency conflict, and reboot pending are distinct outcomes.
- Logs use INF/package identity, signer, old/new version, redacted device identifiers, and operation ID.
- Synthetic tests cover signed package fixtures, unsigned refusal, rollback after failed read-back,
  missing/corrupt package, cancellation, and denied elevation. Live `pnputil /delete-driver` and
  post-reboot verification are a privileged PendingEvidence activity and must never run in the
  ordinary regression suite.

## V5 implementation evidence

- `DriverRemovalEngine` implements the backup, policy, elevation, delete, read-back, and restore
  contract.
- `PnpUtilDriverPackageOperations` is the sole pnputil adapter and deliberately omits `/force`.
- `DriverRemovalEngineTests` provides 7 synthetic acceptance cases without mutating the host.
- UI exposes only an explicit “备份后移除” action; protected/active/unsigned rows are refused.
