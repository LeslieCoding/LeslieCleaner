# CleanMyPC Next Service Manager

## Objective

Inspect and manage Windows services with ownership and elevation safeguards.

## Discovery

Read service configuration, binary path, publisher/signature, start mode, dependencies and current state. Normalize executable paths and accept ownership only when the binary is inside the application fingerprint boundary.

## Operations

Start, stop, enable and disable require explicit confirmation, backup of configuration and security-gate approval. System and security services are review-only unless an elevated user confirms a documented policy.

## Acceptance

Test query, dependency ordering, access denial, protected service, timeout, rollback and stale ownership evidence.

## Implementation dossier

- IDs: `LCN-SERVICE-MANAGER-001..009`; inputs are service name, configuration snapshot, dependency graph, requested action, and confirmation.
- Output includes state, startup type, dependency impact, recovery settings, operation result, and rollback action.
- Service configuration and control require elevation plus a security-gate decision; reads are best effort.
- Dependency conflict, timeout, access denial, protected service, and missing service remain separate failure kinds.
- Logs record service identity, old/new state, dependency snapshot, timeout, elevation, and rollback ID.
- Tests cover dependencies, boot-critical refusal, stop timeout, restore, duplicate request, and elevation denial.
