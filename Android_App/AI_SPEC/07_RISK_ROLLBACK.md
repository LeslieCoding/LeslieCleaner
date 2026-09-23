# LeslieCleaner Android 2.0.0 — Risk and Rollback

## Risks

- R2: public Android API and OEM package visibility return partial or missing facts.
- R2: health data can be stale or unavailable while the app is backgrounded.
- R2: UI state integration can regress scan/result/history behavior.
- R1: version/package metadata or docs become inconsistent.

## Controls

- Label provider-visible coverage and unavailable metrics.
- Keep readers per-fault tolerant and UI non-blocking.
- Preserve old audit fields and snapshot compatibility.
- Run focused tests before full build and OnePlus-only smoke after install.

## Rollback

For code: restore the pre-2.0.0 Android_App source and versionCode 5/versionName 1.2.0. For local
data: do not delete the old snapshot; migration failure leaves it intact. For APK: install the prior
1.2.0 debug APK through the same shell-owned OnePlus session. No production or irreversible action is
authorized by this scope.
