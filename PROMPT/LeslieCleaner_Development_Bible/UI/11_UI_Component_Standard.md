# CleanMyPC Next UI Component Standard

## Required states

Every interactive control defines normal, hover, pressed, focused, disabled, loading and error states. State changes must be announced to assistive technology where user-visible.

## Data controls

Lists and tables provide selection, select-all, sort, filter, paging/virtualization, empty, loading and partial-failure states. Bulk actions disclose selected count, estimated size and risk before confirmation.

## Dialogs

Dialogs have a clear title, consequence text, default-safe focus, cancel path and non-blocking progress. Destructive confirmation cannot be accepted by accidental default focus.

## Acceptance

Component tests cover keyboard-only operation, automation properties, long localized text and rapid repeated clicks.

## Implementation dossier

- IDs: `LCN-UI-COMPONENT-001..012`; each control documents inputs, commands, visual states, accessibility name, and error behavior.
- Buttons expose normal, hover, pressed, focused, disabled, loading, and danger states without layout shift.
- Lists use stable keys, virtualization, selection preservation, empty states, and incremental progress for large data.
- Command controls guard duplicate invocation and surface permission/rollback information before destructive actions.
- Tests cover keyboard, screen reader properties, repeated click race, long localization, and DPI scaling.
