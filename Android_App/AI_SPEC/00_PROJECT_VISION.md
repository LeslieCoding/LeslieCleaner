# LeslieCleaner Android 3.0.0 — Project Vision

## Product intent

LeslieCleaner Android 3.0.0 is a Chinese, privacy-first, read-only device maintenance center.
It helps a user understand storage pressure, junk candidates, app security signals, startup
declarations, device health, and safe next actions.

The product is not a silent cleaner, malware verdict engine, root utility, or remote administration
tool. Android public APIs and OEM package visibility determine the verified scope.

## User value

- Reduce uncertainty: show a trustworthy device health snapshot instead of an empty scan button.
- Reduce investigation time: combine application inventory, device resources, battery state, and
  repeat-scan change analysis in one readable result boundary.
- Preserve control: every operation is read-only, local, bounded, cancellable in design, and
  explained in Chinese.
- Build a durable foundation: separate contracts, readers, analysis rules, audit records, and UI
  presentation so future modules can be added without rewriting the dashboard.

## 3.0.0 success definition

The OnePlus PKG110/API 36 acceptance environment can launch the app, run a complete analysis,
display real metrics, preview junk categories, inspect security/startup signals, hand off to trusted
system pages, persist only bounded redacted audit data, compare repeat scans, and recover from
provider failures without a crash or destructive side effect.

## Product principles

1. Evidence before conclusion.
2. Read-only by default and by implementation.
3. Chinese copy for every user-facing state.
4. Existing visual hierarchy remains recognizable; functionality and information quality grow.
5. Every new signal has a source, uncertainty, test, and rollback path.
