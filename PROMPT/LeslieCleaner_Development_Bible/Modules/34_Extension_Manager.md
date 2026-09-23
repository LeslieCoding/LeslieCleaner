# CleanMyPC Next Extension Manager

## Objective

Inventory browser and application extensions with a transparent trust and cleanup model.

## Discovery

Read extension manifests from supported profile roots, validate IDs, publisher, permissions, version and install source. Keep browser profile boundaries and never upload manifest contents without consent.

## Operations

Disable or remove only selected extensions after profile backup. Unknown publishers and broad permissions receive elevated risk, not automatic deletion.

## Acceptance

Test malformed manifests, duplicate IDs, profile locks, permission scoring, backup/restore, cancellation and cross-profile isolation.

## Implementation dossier

- IDs: `LCN-EXTENSION-MANAGER-001..008`; inputs are host, profile, extension ID, manifest, signer, and requested action.
- Output includes extension identity, permissions summary, signer state, affected profiles, and reversible result.
- Only owned profiles are writable. Extension code is never executed while inspecting its manifest.
- Malformed manifest, locked profile, unsupported host, unsigned package, and missing extension are explicit outcomes.
- Logs record host/profile/extension ID, manifest hash, permissions summary, old/new state, and rollback ID.
- Tests cover multiple profiles, malformed manifests, locks, disable/restore, cancellation, and unsupported hosts.
