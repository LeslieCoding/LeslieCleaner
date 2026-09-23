# LeslieCleaner Android 3.0.0 — Scope and Non-goals

## In scope

1. Analysis orchestration: one guarded, background analysis command with progress and result state.
2. Device health snapshot: storage capacity/free space, memory totals/available memory, battery
   percentage/charging state when public APIs provide them.
3. Application inventory: package identity, label, version, target SDK, update time, system/user,
   enabled/disabled, debuggable, and fact-based attention explanation.
4. Repeat analysis: hashed application identity snapshots and aggregate delta counts.
5. Local evidence: bounded JSONL audit history, redacted export, clear-history confirmation.
6. Chinese result and detail boundaries within the existing dashboard/navigation shell.
7. Unit, instrumented, OnePlus-only smoke, and evidence documentation.
8. Junk management preview with measurable categories and system/OEM cleanup handoff.
9. Security center with local static signals and Play Protect/system security handoff.
10. Startup center with visible boot declarations and user-controlled settings handoff.
11. System acceleration combining memory pressure, storage pressure, and safe next actions.
12. Offline-first signed/versioned cloud-rule update contract with bundled fallback.

## Out of scope for 2.0.0

- Deleting applications, files, caches, photos, messages, or user content.
- Root, hidden APIs, accessibility abuse, device-owner APIs, VPN interception, or remote commands.
- Network upload, cloud account, paid service, AI provider, credentials, or background daemon.
- Claiming malware detection, battery-life guarantees, performance guarantees, or complete device coverage.
- Replacing the established visual identity or adding a second navigation system.
- Claiming heuristic analysis is equivalent to antivirus or silently removing a virus.
- Controlling arbitrary third-party autostart switches where Android/OEM has no public API.
- Contacting an unapproved cloud endpoint or uploading app/device identifiers without review.
- Testing or modifying any connected device other than OnePlus serial 92f73a16.

## Release boundary

All enhancements must remain R2 at most: local, reversible, read-only, and covered by tests. A
request for actual cleanup becomes a separate REG change requiring new scope, risk analysis,
confirmation, backup/quarantine, read-back, and rollback design.
