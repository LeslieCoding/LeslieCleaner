# CleanMyPC Next Favorites

## Objective

Persist user-selected rules and targets safely across restarts.

## Rules

Use stable IDs plus canonical paths, not display names. Adding is idempotent; removing one favorite removes exactly that identity. Missing or changed targets are marked stale and require review.

## Persistence

Use atomic writes, bounded collections, corruption recovery and per-user storage. Never favorite a protected target without showing its risk and permission state.

## Acceptance

Test duplicate add, exact remove, path casing, rename, corrupt store, concurrent writes, restart and stale-target display.

## Implementation dossier

- IDs: `LCN-UX-FAVORITES-001..008`; favorites use stable normalized identity, display metadata, target type, and last-known state.
- Add/remove is idempotent and atomic; removal matches exact identity rather than a text prefix.
- Corrupt storage is isolated, backed up for diagnosis, and recovered into a usable empty state with a visible warning.
- Stale targets remain visible with a repair/remove action instead of disappearing silently.
- Tests cover duplicate add, casing, rename, corrupt file, concurrent writes, restart, stale target, and exact remove.
