# AI Assistant Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent with `Modules/35_AI_Assistant.md`, `AI/50_AI_Rules.md`, and `00_Execution_Contract.md`.

## Mission

Implement an optional AI explanation layer. It summarizes evidence, risks, expected benefit, and recovery choices. It never becomes a system-maintenance executor and never receives raw private content that is unnecessary for the answer.

## Context contract

Host context is typed and minimal: finding IDs, module/category, rule/evidence IDs, counts, bytes, risk, confidence, permission state, backup state, and operation history summary. Redact account names, full paths, emails, tokens, cookies, document names, package secrets, and browser/database contents before constructing a prompt.

Provider context is separated from local deterministic policy. Unsafe requests, missing evidence, or provider failures use a local answer that says what is known and unknown.

## Response schema

Validate provider responses into:

```text
RequestId, Summary, Recommendations[], EvidenceIds[], Confidence,
Risk, RequiresConfirmation, PermissionNeed, RollbackRoute,
Limitations[], NextSafeAction
```

Reject output that invents evidence IDs, includes commands for direct destructive action, asks for secrets, tries to override security policy, or exceeds context budget.

## Safety and UX

Every recommendation links to evidence and explains uncertainty. “Delete now” is invalid; the next action must navigate into the normal preview/confirmation/authorization workflow. Provider/model selection is opt-in, HTTPS-only when remote, timeout-bound, budget-bound, and shown in settings.

History stores only redacted request/response summaries with retention limits. Diagnostics record provider class, timing, validation, safety decision, and redaction counts, not personal content.

## Tests

Cover offline mode, timeout, cancellation, invalid JSON/schema, prompt injection, unsafe deletion command, fabricated evidence, secret/path redaction, history retention, Chinese/English rendering, and navigation to a reviewed action.

## Handoff

Report local vs remote behavior, context fields allowed, schema validation, redaction tests, provider evidence gaps, and confirmation boundaries.
