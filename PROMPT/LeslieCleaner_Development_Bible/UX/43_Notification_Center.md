# CleanMyPC Next Notification Center

## Objective

Deliver concise, non-blocking status while preserving actionable errors.

## Rules

Notifications include severity, title, summary, timestamp, operation link, progress and dismiss behavior. Errors include cause classification and next step. Destructive completion is announced only after verification.

## Reliability

Notifications are deduplicated by operation ID, bounded in memory and safe across page navigation. Accessibility announcements use live regions without stealing focus.

## Acceptance

Test queue overflow, duplicate events, cancellation, failure, localization, screen reader announcements and restart recovery.

## Implementation dossier

- IDs: `LCN-UX-NOTIFY-001..009`; notifications have stable operation ID, severity, dedupe key, expiry, action, and accessibility text.
- Success, partial, failure, cancellation, restart-needed, and rollback-available are distinct visual and spoken states.
- High-volume progress events are coalesced; critical security or data-recovery messages cannot be silently dropped.
- Actions route to the current operation/history record and reject stale or expired commands.
- Tests cover overflow, dedupe, localization, screen reader announcement, restart recovery, and action safety.
