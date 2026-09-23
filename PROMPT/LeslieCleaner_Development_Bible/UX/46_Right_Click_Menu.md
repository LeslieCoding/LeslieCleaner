# CleanMyPC Next Context Menu

## Objective

Expose relevant actions without bypassing the same service-layer safety gates.

## Actions

View details, reveal source, add/remove favorite, exclude, verify, backup, restore and request cleanup are context-aware. Unsupported or protected actions are disabled with a reason rather than hidden silently.

## Acceptance

Test keyboard invocation, selection consistency, permission denial, protected targets, stale rows, localization and no-action-on-open behavior.

## Implementation dossier

- IDs: `LCN-UX-CONTEXT-001..008`; menu commands bind to stable IDs and evaluate current permission/protection state when opened.
- Opening a menu is side-effect free; destructive commands remain disabled until confirmation and authorization complete.
- Every item has localization, keyboard access, automation name, visible disabled reason, and stale-selection handling.
- Protected/denied targets show the safe next action such as elevation request, exclusion, or documentation.
- Tests cover pointer/keyboard invocation, stale rows, permission denial, localization, and no-action-on-open.
