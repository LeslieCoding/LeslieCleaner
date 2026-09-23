# Startup Manager Implementation Prompt

Version: 2.0  
Use: Give this prompt to an implementation agent with `Modules/25_Startup_Manager.md` and `00_Execution_Contract.md`.

## Mission

Build a reversible Windows startup manager that explains what starts, where it is registered, who signed it, its risk/impact evidence, and whether it can safely be disabled. It must not use a simplistic “all non-Microsoft entries are bad” rule.

## Discovery sources

Discover with stable source identity from:

- HKCU/HKLM Run and RunOnce in explicit Registry32/Registry64 views.
- User/common Startup folders.
- Task Scheduler tasks, triggers, actions, owners, and run state.
- Services, drivers, start types, dependencies, and recovery configuration.
- Approved delayed-start mechanisms and application-specific startup providers.

Keep source, physical location, normalized command, resolved executable, publisher, signature state, version, launch condition, and access state on every item.

## Classification

Impact is an explainable score, not a deletion recommendation. It may consider measured startup duration, CPU/disk/network evidence, launch frequency, publisher/signature, user choice, and essential-system policy. The UI must show factors and confidence, not only a colored label.

Microsoft-signed, boot-critical, security, driver, service dependency, and policy-managed items are protected by default. Unknown signature does not automatically mean malicious.

## State changes

Disable/restore is preferred over delete. Before change, capture exact source state: registry data/casing/view, shortcut location, task XML, service start type, or provider manifest. After action, read back the same source and keep a rollback record.

Machine-wide changes require elevation. If elevation is denied, retain the finding and show the precise boundary. Do not modify ACLs to bypass it.

## Tests

Cover 32/64 Run views, duplicate command sources, quoted arguments, missing executable, signature unavailable, protected Microsoft entry, task XML preservation, service dependencies, driver refusal, UAC denial, disable/restore, cancellation, and repeated action idempotence.

## Handoff

Report source coverage, scoring evidence, protected policies, reversible actions, test results, and any ETW/long-term startup measurement gaps.
