# CleanMyPC Next AI Assistant Module

## Objective

Explain system state and recommend safe maintenance actions using minimal aggregate context.

## Capabilities

Provide storage, performance, startup, privacy, security, registry and cleanup explanations; summarize scans; generate reports; suggest rollback and history trends. AI is advisory only and delegates execution to typed non-AI services.

## Safety

Local rules are the default. Remote providers are optional, HTTPS-only, redacted and timeout-bound. Dangerous intent and dangerous output are rejected; credentials are stored through a secret reference.

## Acceptance

Test every category, evidence citations, redaction, offline fallback, timeout, malformed response, cancellation, history limits and refusal of direct destructive commands.

## Implementation dossier

- IDs: `LCN-AI-ASSISTANT-001..012`; inputs are aggregate evidence, question, provider policy, and cancellation.
- Output is typed: explanation, recommendation, evidence IDs, confidence, risk, confirmation requirement, and next action.
- AI may explain and rank only; operation services remain the sole authority to execute changes.
- Remote provider access is opt-in, HTTPS-only, redacted, timeout-bound, and budget-bound.
- Offline, timeout, malformed output, unsafe intent, and provider denial have deterministic local fallback.
- Logs include request ID, provider class, redaction count, latency, validation, and safety decision.
- Tests cover dangerous prompts, secret redaction, invalid JSON, timeout, cancellation, offline mode, and history export.
