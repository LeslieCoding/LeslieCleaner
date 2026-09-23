# CleanMyPC Next Task Scheduler Module

## Objective

Inspect scheduled tasks and manage only entries proven to belong to the selected application.

## Discovery

Parse task XML actions, triggers, principal, author, publisher and executable path. Reject ambiguous command prefixes and reparse escapes; classify Microsoft, OEM, application and unknown tasks.

## Operations

Disable/delete requires a task XML backup, explicit confirmation and elevation guidance. Preserve dependencies and report running instances separately.

## Acceptance

Test XML parsing, quoted arguments, environment variables, ownership collisions, protected tasks, restore, timeout and cancellation.

## Implementation dossier

- IDs: `LCN-TASK-MANAGER-001..008`; inputs are task path/name, XML identity, requested state, trigger policy, and confirmation.
- Output includes owner, triggers, actions, current state, change result, and exact restore XML/hash.
- User tasks stay account-scoped; machine tasks require elevation and task XML is never executed during inspection.
- Malformed XML, missing task, protected task, dependency issue, and access denial are distinct failures.
- Logs include task identity, old/new state, XML hash, elevation, and rollback record.
- Tests cover XML round-trip, trigger preservation, protected refusal, duplicate action, timeout, cancellation, and restore.
