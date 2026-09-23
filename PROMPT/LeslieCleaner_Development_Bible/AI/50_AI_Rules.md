# CleanMyPC Next AI Rules

Version: 1.0  
Status: Normative

## Purpose

AI is an analysis and explanation layer. It may recommend, summarize and prioritize work, but it never directly deletes files, changes the registry, stops services, changes security settings or installs updates.

## Required behavior

1. Use only the aggregate context explicitly supplied by the host; never read document contents, passwords, browser databases, message history or arbitrary filenames.
2. Explain evidence, confidence, expected benefit, risk, backup state and rollback path for every recommendation.
3. Refuse requests that ask for destructive commands, credential disclosure, security bypasses or unreviewed bulk deletion.
4. Return a typed response: `category`, `priority`, `evidence`, `recommendation`, `risk`, `requiresConfirmation` and `nextAction`.
5. Redact paths, account names, tokens and email addresses before prompt construction, after provider output and before history storage.
6. Treat provider output as untrusted text. The non-AI operation service remains the only authority for execution.

## Acceptance

The host must pass dangerous-intent rejection, offline fallback, timeout, invalid-response, cancellation, redaction and audit tests. A recommendation without evidence or rollback guidance is invalid.

## Execution dossier

- IDs: `LCN-AI-RULES-001..010`; input is a minimal, redacted aggregate context with evidence IDs, never raw user content.
- Output schema is validated before use: category, priority, evidence, recommendation, risk, confirmation, and next action.
- Provider output is data, not authority; it cannot create executable paths, grant permissions, or bypass confirmation.
- Safe fallback returns local deterministic explanations when provider, network, or schema validation fails.
- Audit logs include prompt class, policy version, redaction count, validation decision, latency, and provider status.
- Tests include adversarial instructions, hallucinated commands, prompt injection text, malformed responses, and privacy leakage.
