# CleanMyPC Next Startup Manager

## Objective

Manage startup entries by source and ownership, not merely by registry location.

## Sources

Classify Microsoft components, OEM hardware, drivers, user applications and unknown entries using publisher, signature, path and protected-resource evidence. Show the evidence and a startup-impact score.

## Operations

Disable/enable only after backup and confirmation. Registry Run, startup folders, scheduled tasks, services and drivers use separate adapters and explicit elevation guidance. Unknown or protected items default to review-only.

## Acceptance

Test source classification, signed Microsoft protection, 32/64-bit Run entries, task/service/driver permission failure, restore, cancellation and repeated toggling.

## Implementation dossier

- IDs: `LCN-STARTUP-MANAGER-001..012`; inputs are source, stable identity, current state, signature evidence, and requested action.
- Pipeline: enumerate → canonicalize → verify signature → score impact → preview → authorize → disable/restore → verify.
- Output includes source, command/path, publisher, signature state, impact score, reversible action, and reboot requirement.
- Machine Run keys, services, drivers, and machine tasks require elevation; user Run entries remain user-scoped.
- Logs record source, identity, old/new state, signature result, elevation, and post-action verification.
- Tests cover duplicate source, delayed task, dependency conflict, missing path, protected entry, restore, cancellation, and repeated toggling.
