# CleanMyPC Next Testing Standard

Version: 1.0  
Status: Normative

## Test layers

1. Core unit tests validate pure rules, scoring, normalization and policy decisions.
2. Infrastructure integration tests use isolated temporary roots, registry keys and local HTTP fixtures.
3. Module tests validate real backup/restore, cancellation, retry and post-operation verification.
4. UI contract tests validate navigation, accessibility properties, dispatcher ownership and actionable error text.
5. Release tests validate self-contained publishing, integrity baseline provisioning, installation, upgrade and uninstall.

## Required case matrix

Every destructive module must test success, missing target, access denied, protected target, in-use target, cancellation, timeout, duplicate request, backup corruption and rollback. Every scanner must test false positive, false negative, duplicate result, stale cache and inaccessible item accounting.

## Evidence

Tests record environment, sample size, elapsed time, P50/P95 where relevant, skipped conditions and artifact paths. A skipped test is not a pass. Tests never use production paths or unbounded full-disk recursion.

## Test dossier

- IDs: `LCN-TEST-001..014`; test names include module, behavior, and expected result.
- Fixtures use temporary roots, unique registry namespaces, fake clocks, local HTTP endpoints, and deterministic provider responses.
- Every scanner has a false-positive and conflicting-identity case; every mutator has a read-back and rollback case.
- Tests report skipped capabilities as `PendingEvidence` with environment reason, never as success.
- Release tests are isolated from ordinary tests because installer/UAC/device work may require a clean Windows image.
