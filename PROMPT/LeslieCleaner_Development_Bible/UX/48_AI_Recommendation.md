# CleanMyPC Next AI Recommendation UX

## Objective

Turn aggregate telemetry into understandable, reviewable maintenance suggestions.

## Presentation

Each card shows category, priority, evidence, estimated benefit, risk, confidence, affected scope and a safe next action. “Why” expands the rule or metric source. AI cannot execute the action from the card.

## Safety

High-risk suggestions require details, backup status and explicit confirmation before the normal operation service can be invoked. Offline rules remain usable when a provider is unavailable.

## Acceptance

Test explainability, redaction, provider failure, stale metrics, dangerous output, localization, keyboard navigation and confirmation boundaries.

## Implementation dossier

- IDs: `LCN-UX-AI-REC-001..010`; input is redacted evidence, current metrics, policy, and user intent.
- A recommendation presents evidence links, confidence, risk, expected effect, permission, confirmation, and rollback route.
- AI output cannot become an executable action; the user enters the normal preview/authorization pipeline.
- Stale metrics and provider failure display deterministic local explanation rather than fabricated certainty.
- Tests cover explainability, redaction, unsafe output, stale data, localization, keyboard operation, confirmation, and offline mode.
