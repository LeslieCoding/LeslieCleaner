# Privacy and Browser Cleaner Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent with `Modules/26_Privacy_Guard.md`, `Modules/27_Browser_Cleaner.md`, and `00_Execution_Contract.md`.

## Mission

Implement privacy and browser cleaning that is conservative by default. The product must clearly distinguish cache cleanup from identity/session removal, and must never silently log a user out or remove credentials.

## Data policy

Default-cleanable categories: cache objects, GPU/shader cache, crash reports, temporary logs, thumbnail/cache databases where provider-safe, and explicitly selected history only when the user confirms.

Protected categories: passwords, payment/autofill data, cookies, login tokens, active sessions, extension secrets, bookmarks, downloads list unless confirmed, browser profile settings, documents, and other personal content.

Every provider rule declares category, schema/version applicability, lock/process requirement, owner scope, backup behavior, and whether it can change a signed-in state.

## Provider workflow

`Discover provider → Enumerate owned profiles → Verify profile ownership → Detect browser/editor lock → Load cache-only rules → Measure → Preview protected/excluded data → Confirm → Backup/quarantine if applicable → Clean → Verify`.

Do not write into another user’s profile. Do not force-close processes as part of an ordinary cache cleanup. When a lock prevents a safe cleanup, show skipped items and a restart recommendation.

## Privacy UX

Preview must state time range, provider/profile, category, whether active session is protected, item count, estimated bytes, backup/rollback availability, and restart requirement. Deleting history/cookies requires category-specific confirmation; a generic “clean all” button cannot bypass it.

## Tests

Use synthetic Chromium/Firefox/WebView profiles. Cover multiple accounts, foreign profile denial, locked database, stale schema, cookie/session protection, time range, tracking whitelist, backup/restore, cancellation, partial result, and localized disclosure.

## Handoff

Report cache-only providers, protected categories, schema coverage, lock behavior, tests, and any live-browser matrix that remains manual evidence.
