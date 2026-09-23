# Decision Log

## D-001 — Preserve all source files through a `Legacy/` Android prompt area

- Classification: AI_RECOMMENDED
- Reason: The existing 83-file adaptation preserves the Bible subtree; five top-level source prompts also require traceable Android counterparts.
- Consequence: `PROMPT_MANIFEST.json` now contains 88 entries and the five legacy files retain source content after an Android boundary contract.

## D-002 — Start with read-only UI

- Classification: AI_RECOMMENDED / safety
- Reason: Android storage and package mutation require provider-specific contracts and user/system confirmation.
- Consequence: The first app slice has no permissions and no destructive operation.

## D-003 — Treat PackageManager visibility as provider capability, not full inventory proof

- Classification: AI_RECOMMENDED / safety
- Reason: Android package visibility and OEM policy can limit results without a safe way to infer the missing applications.
- Consequence: `Verified` means the returned provider result was read successfully; it does not claim complete device coverage.

## D-004 — Persist only minimal scan audit facts

- Classification: AI_RECOMMENDED / privacy
- Reason: Future support investigations need event evidence without retaining raw package paths, document URIs, or personal content.
- Consequence: JSONL records contain event, result, count, coverage, and typed failure kind only; each write is flushed synchronously.

## D-005 — Bound local history before adding sharing

- Classification: AI_RECOMMENDED / privacy
- Reason: An unbounded audit file is unnecessary for the foundation and complicates privacy review.
- Consequence: The logger retains at most 200 records; export remains redacted text until a confirmed Android share flow is added.

## D-006 — Share audit data only through a temporary FileProvider URI

- Classification: AI_RECOMMENDED / privacy
- Reason: Android apps must not expose private filesystem paths or grant broad storage access for export.
- Consequence: A user-confirmed chooser receives only a cache-scoped redacted JSONL file with read permission.

## D-007 — Redesign the foundation dashboard without inventing health scores

- Classification: USER_EXPLICIT (UI quality) + AI_RECOMMENDED
- Reason: The original page was visually flat and the foundation has no evidence for a device health score.
- Consequence: The new screen emphasizes verified/read-only state, scan evidence, capability cards, audit history, and an honest settings placeholder instead of fabricated metrics.

## D-008 — Use a native adaptive launcher icon for the Android brand

- Classification: USER_EXPLICIT (visual quality) + AI_RECOMMENDED
- Reason: The default launcher artwork made the app look unfinished; the app needs a crisp icon that remains legible across Android launcher shapes.
- Consequence: Added a blue/purple adaptive background and white shield-check vector foreground, wired to both `icon` and `roundIcon`; no bitmap or third-party asset dependency was added.

## D-009 — Replace the shield motif with a cleaning-motion mark

- Classification: USER_EXPLICIT (visual revision)
- Reason: The first shield-check concept read as a generic security utility and did not communicate cleaning or polish strongly enough.
- Consequence: The launcher now uses a dark indigo-to-purple background with cyan/lilac sparkles and white sweeping motion bands, while keeping the same native adaptive-icon contract.

## D-010 — Refine the launcher into a restrained premium monogram

- Classification: USER_EXPLICIT (visual quality)
- Reason: The sparkle-sweep revision still read as a generic utility illustration rather than a considered product identity.
- Consequence: Reduced the palette and geometry to a graphite/indigo field, ivory Leslie “L” mark, and one champagne sparkle; retained the adaptive vector implementation for crisp launcher rendering.

## D-011 — Replace the monogram with a faceted crystal badge

- Classification: USER_EXPLICIT (visual revision)
- Reason: The monogram direction was still not meeting the requested app-icon aesthetic.
- Consequence: The icon now uses a centered faceted crystal silhouette with restrained ivory, violet, mint, and champagne accents on a midnight gradient; it remains a native adaptive vector icon.

## D-012 — Restore the preferred L mark with a blue base

- Classification: USER_EXPLICIT (selected visual direction)
- Reason: The user preferred the prior Leslie L monogram over the crystal badge, but requested a blue visual foundation.
- Consequence: Restored the L-and-sparkle foreground and changed the adaptive background to a deep navy-to-royal-blue gradient.

## D-013 — Lift the blue base for a brighter launcher presence

- Classification: USER_EXPLICIT (color refinement)
- Reason: The selected blue-base icon was visually too dark on the launcher.
- Consequence: Kept the L monogram and champagne sparkle unchanged while shifting the background to a lighter blue gradient (`#1B4F96` → `#4D8DE2`).

## D-014 — Push the lighter blue base toward blue-purple

- Classification: USER_EXPLICIT (color refinement)
- Reason: The lighter blue needed more blue saturation and a subtle purple bias while retaining a blue-first identity.
- Consequence: Updated the adaptive background to `#2851C4` → `#725CDF`; the foreground mark remains unchanged.

## D-018 — Keep inventory interaction inside the existing visual shell

- Classification: USER_EXPLICIT (UI unchanged) + AI_RECOMMENDED
- Reason: The prompt requires search, filtering, details, and large-list performance, while the user explicitly asked not to redesign the UI.
- Consequence: Added the capabilities inside the existing scan-result dialog, using search/filter controls, a read-only detail dialog, and LazyColumn virtualization; homepage hierarchy and navigation remain unchanged.

## D-019 — Persist only hashed inventory identities for scan comparison

- Classification: AI_RECOMMENDED (privacy-preserving capability)
- Reason: The prompt calls for repeat scans and difference analysis, but raw package names should not be added to persistent audit or snapshot data.
- Consequence: `InventorySnapshotStore` compares SHA-256 identity fingerprints and versions in app-private storage; audit metadata contains only counts and flags.

## D-020 — Explain observable app signals without making security verdicts

- Classification: AI_RECOMMENDED (bounded read-only analysis)
- Reason: public PackageManager facts cannot prove that an application is malicious or safe.
- Consequence: details label debuggable/disabled states as attention signals, include target SDK and update time, and avoid remediation or malware claims.

## D-015 — Show an explicit result boundary for homepage scanning

- Classification: USER_EXPLICIT (bug fix) + AI_RECOMMENDED
- Reason: The first scan path only changed small inline text, so a successful or failed scan looked like no action to the user.
- Consequence: The scan now uses API-level package flags, isolates per-package failures, records the audit event, and opens a visible result dialog with count, labels, versions, and a clear provider-visible scope statement.

## D-016 — Make secondary feature states explicit during smoke testing

- Classification: AI_RECOMMENDED (verification) + USER_SCOPE
- Reason: The application-list card had an empty click handler, and export could fail on devices without a share handler without leaving a failure event.
- Consequence: The application-list card now opens the latest-result boundary (or explains that a scan is required); export uses `text/plain` for broader compatibility and records `NoShareHandler` or the runtime failure kind.

## D-017 — Complete privacy and settings as bounded read-only controls

- Classification: USER_SCOPE + AI_RECOMMENDED (safe implementation)
- Reason: Privacy signals and preferences were still placeholder cards despite their underlying contracts already being defined.
- Consequence: Privacy now explains permissions, Root/API boundaries, and local retention; settings exposes read-only protection, 200-record retention, and a confirmed clear-local-history operation. No device-wide deletion or new permission was introduced.
