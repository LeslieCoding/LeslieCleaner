# CleanMyPC Next AI Logging Rules

Version: 1.0  
Status: Normative

## Event contract

Every scan and operation records start, finish, cancellation or failure. Required fields are `EventType`, `OperationId`, `Module`, `StartedUtc`, `DurationMs`, `Result`, `ItemCount`, `SizeBytes`, `FailureKind` and `Elevated` when applicable.

## Privacy and durability

Logs are UTF-8 structured files under the configured local log directory. Paths, account names, tokens, cookies, emails and document contents are redacted. Writes are asynchronous but bounded with backpressure; shutdown flushes with a finite timeout. Rotation and retention are explicit and observable.

## Acceptance

Tests verify concurrent writes, crash-safe rotation, UTF-8 Chinese text, exception details, redaction, shutdown flush, malformed records and diagnostic export integrity.

## Logging dossier

- IDs: `LCN-LOG-001..010`; event schemas are versioned and include `OperationId`, `RequirementId`, module, stage, result, duration, and failure kind.
- Start, decision, item result, finish, cancellation, and failure are distinct events; a finish event alone cannot imply success.
- Logs are bounded, rotated, retention-managed, flush on shutdown with finite timeout, and preserve UTF-8 localization.
- Diagnostic export contains integrity metadata and redacts secrets/personal data again at export time.
- Performance tests measure producer backpressure so logging cannot silently exhaust memory during scans.
