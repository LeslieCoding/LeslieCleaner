# Decision Log — LeslieCleaner Android 3.0.0

## D-3-001 — Expand into a phone maintenance center
- Source: USER_EXPLICIT
- Decision: add junk/cache, security, startup, acceleration, app inventory and rule-update modules while preserving the Chinese blue-purple visual shell.

## D-3-002 — System-mediated destructive actions
- Source: AI_RECOMMENDED / Android platform boundary
- Decision: show preview and route to Android/OxygenOS settings; never silently clear other apps, force-stop processes, or claim malware removal.

## D-3-003 — Offline-first security rules
- Source: AI_RECOMMENDED
- Decision: ship bundled rules and a SHA-256 verification contract. Live cloud updates require an approved endpoint, TLS/signing key, privacy text and a new REG.

## D-3-004 — OnePlus evidence only
- Source: USER_EXPLICIT
- Decision: all device commands use `adb -s 92f73a16`; other devices are out of scope.
