# CleanMyPC Next Settings Module

## Objective

Centralize user preferences without allowing settings to bypass safety policy.

## Settings groups

Expose scan scopes, exclusions, retention, language, theme, update channel, AI provider reference, privacy modes, notification policy and performance sampling. Every setting has a validated type, default, migration rule and reset behavior.

## Persistence

Write atomically, encrypt secrets through the secret store, reject corrupt or unknown values safely and log migrations without logging sensitive values. Changes are observable through a typed settings service.

## Acceptance

Test defaults, invalid values, concurrent writes, cancellation, migration, reset, secret redaction and hot reload behavior.

## Implementation dossier

- IDs: `LCN-SETTINGS-001..010`; inputs are typed key/value, schema version, scope, validation policy, and reset request.
- Output includes effective value, source scope, migration result, restart requirement, and audit entry.
- User settings are user-scoped; machine policy is read-only unless a specific elevated workflow authorizes it.
- Malformed value, corrupt file, migration conflict, denied write, and unknown key are typed failures.
- Logs identify setting key and schema version but never serialize secret values.
- Tests cover atomic interruption, corrupt JSON, migration, redaction, reset, localization, hot reload, and concurrent update.
