# CleanMyPC Next AI Development Workflow

Version: 1.0  
Status: Normative

## Delivery loop

1. Select a requirement from the single-source index and assign an ID.
2. Write the acceptance cases before implementation.
3. Change the smallest responsible layer and preserve public compatibility.
4. Run focused tests, then ordinary full regression, then release smoke tests.
5. Record evidence, limitations, changed files and rollback instructions.
6. Update the requirement status only after the evidence artifact exists.

## AI operating rules

The AI must inspect the current repository before editing, avoid speculative rewrites, preserve user changes, use the existing abstractions and report blockers with evidence. It must never mark a feature complete because a file or page exists.

## Release rule

No release candidate may contain a known security, data-loss, silent-failure or unbounded-scan defect. Deferred work is tracked with owner, priority, test gap and next executable action.

## Workflow dossier

- IDs: `LCN-WORKFLOW-001..010`; work begins with a requirement ID and ends with source, tests, evidence, and limitation updates.
- The AI reads current code before editing, makes the smallest responsible change, then re-reads the diff for safety and scope.
- Delivery order is focused tests → broad regression → build → installer/device smoke when applicable.
- A failed or unavailable external test is recorded explicitly with the exact command and environmental boundary.
- The release report separates implemented behavior, partial behavior, pending evidence, and intentionally deferred work.
