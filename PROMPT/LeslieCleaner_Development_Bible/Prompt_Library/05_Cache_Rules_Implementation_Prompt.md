# Cache Rules and Smart Scan Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent with `Modules/20_Smart_Scan.md`, `Modules/27_Browser_Cleaner.md`, and `00_Execution_Contract.md`.

## Mission

Build a versioned Cache Rules system for Windows 10/11 that locates safe-to-clean data directly. It must cover Windows caches, browsers, developer tools, games, creative software, and AI clients without default full-volume scanning.

## Rule model

Every rule has `RuleId`, `Module`, `Provider`, `Category`, `Platform`, `PathTemplate`, `Include`, `Exclude`, `OwnershipPredicate`, `Risk`, `DefaultAction`, `RequiresProcessClosed`, `MinAppVersion`, `MaxAppVersion`, `RuleVersion`, `LastVerifiedUtc`, and `TestFixture`.

Rules are deterministic data, not scattered path literals. Rule changes require code review, fixture update, version bump, changelog entry, and a false-positive test.

## Provider families

Prioritize direct rules for:

1. Windows temp, logs, crash dumps, thumbnails, update/download caches, Delivery Optimization, shader caches, GPU/font caches, and recycle-bin metadata.
2. Chromium, Edge, Firefox, WebView, Arc, and other browser cache-only locations.
3. VS Code, Cursor, JetBrains, Visual Studio, Node package/cache tools, Python package/cache tools, Java tooling, Docker, WSL, and Hyper-V diagnostics.
4. Steam, Epic, Battle.net, Unity, Unreal, Adobe, Office, QQ, WeChat, and other provider-specific cache/log locations.
5. ChatGPT desktop, Claude desktop, AI editor, local-model, and AI-plugin caches only after profile ownership and category policy checks.

The rule must identify the exact category. “Application data” alone is not a safe category.

## Scan flow

`Load signed/versioned rules → Expand approved templates → Canonicalize → Check ownership/profile → Check lock/process policy → Measure → Exclude protected/session data → Deduplicate → Preview`.

The scan returns direct-location evidence, not merely a filename match. Missing or inaccessible locations are counted per rule and shown as partial coverage.

## Safety defaults

- Cache, temporary logs, crash reports, installer leftovers, and reproducible shader data may be candidates when rules verify ownership.
- Credentials, cookies, browser sessions, saved passwords, documents, project source, game saves, license files, and user-created content are excluded unless a separately confirmed feature owns them.
- Locked databases are skipped by default; do not force-close a browser/editor without a dedicated user-confirmed workflow.
- Network paths, reparse points crossing scope, and other-user profiles are denied by default.

## Outputs and UX

For each finding expose category, provider, rule ID/version, canonical location, estimated bytes, access state, risk, process/lock state, permission, and rollback availability. The UI separates “estimated reclaimable” from “verified freed.”

The preview explains why an item is included and why excluded data is protected. A user can exclude a rule/category for this run; persistent exclusions are versioned settings with recovery/reset behavior.

## Tests and performance

Test template expansion, environment variables, missing roots, path traversal, reparse points, profile ownership, lock handling, exclusions, duplicate rules, stale rules, cancellation, and post-clean verification. Benchmark per-provider rule groups; cache scan is bounded by direct locations and must not invoke deep scan silently.

## Handoff

Report providers/rules added, safe default scope, exclusions, fixture coverage, benchmark evidence, rule version, and providers still `PendingEvidence`.
