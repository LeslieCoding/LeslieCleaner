# AI Prompt Execution Contract

Version: 2.0  
Status: Normative

## Prompt lifecycle

An AI task is a controlled transformation:

`Context → Constraints → Plan → Tool Use → Evidence → Review → Handoff`.

The prompt MUST identify the repository, target requirement IDs, allowed files, forbidden actions, test command, and evidence format. “Improve this module” is not an executable prompt until those fields are present.

## Context rules

The host supplies only the minimum aggregate context required for the task. The model MUST NOT request secrets, browser databases, message bodies, credentials, or arbitrary personal files. Paths are redacted before provider calls and before history persistence.

## Tool rules

Read-only inspection precedes edits. Destructive commands require an explicit operation service, backup, confirmation, security gate, and read-back. The AI may propose an action but cannot invoke deletion, registry mutation, service control, update installation, or device flashing directly.

## Required output

The model returns typed JSON or a schema-equivalent object with `requirementId`, `status`, `evidence`, `risk`, `changes`, `tests`, `limitations`, and `nextAction`. Invalid or unsafe output is rejected and logged.

## Review loop

After implementation, the AI re-reads the diff, runs focused tests, checks cancellation and failure paths, and reports exact commands. A passing build alone is insufficient. Any missing external evidence remains `PendingEvidence`.
