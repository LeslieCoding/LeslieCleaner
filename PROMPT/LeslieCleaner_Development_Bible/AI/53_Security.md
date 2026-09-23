# CleanMyPC Next AI Security Rules

Version: 1.0  
Status: Normative

## Trust boundaries

The UI, AI provider, plugin process, update source and downloaded package are untrusted. Core operation services, the security gate, backup verifier and integrity service are trusted only after startup assessment.

## Rules

- Default policy is deny; permissions are typed by subject, resource, action and expiry.
- Only absolute HTTPS endpoints are accepted for remote providers and updates; credentials never appear in URLs or logs.
- Normalize paths before comparison and reject traversal, junctions and symbolic links crossing a protected boundary.
- Registry operations name the hive and view explicitly, require a matching backup and verify the target after mutation.
- Integrity failure enters read-only mode. Re-establishing a baseline requires explicit authorization and an auditable reason.
- Secrets use DPAPI-protected key material and are never serialized as plaintext configuration.

## Acceptance

Tamper, traversal, reparse-point, unsigned-update, unauthorized-operation and malformed-provider tests must fail closed and emit audit evidence.

## Security dossier

- IDs: `LCN-SECURITY-001..016`; policy decisions contain subject, resource, action, scope, expiry, and reason.
- External inputs include plugins, providers, updates, installer data, registry values, paths, and IPC payloads; each is validated before use.
- Canonical path checks occur before open, before mutation, and after reparse-point-sensitive traversal.
- Integrity failure transitions the application into observable read-only mode until a separately authorized repair succeeds.
- Security events are chained/audited and redact secrets, personal paths, cookies, tokens, and provider credentials.
