# CleanMyPC Next Residual Engine

## Objective

Resolve application residue using a multi-stage bounded pipeline.

## Stages

1. Installation database identity. 2. Installer logs. 3. Uninstall string. 4. Publisher and product code. 5. GUID and package identity. 6. signed known rules. 7. targeted filesystem/registry/service/task checks. 8. opt-in deep scan.

## Data model

Store application identity, evidence source, canonical path, confidence, ownership proof, risk, rule version and last verification. Merge duplicate evidence by canonical identity and preserve provenance.

## Acceptance

Known applications resolve without full-disk recursion; false ownership, publisher-prefix collisions, stale rules, inaccessible locations and cancellation are tested and reported.

## Implementation dossier

- IDs: `LCN-RES-RESIDUAL-001..016`; input includes product, publisher, install location, ProductCode/GUID, rules, and deep-scan opt-in.
- Stage order: install database → known rules → uninstall logs → publisher/product index → bounded deep scan.
- Product identity outranks publisher; Unicode FormKC and whitespace normalization are mandatory.
- Output includes canonical identity, kind, size, source stage, confidence, access state, and explanation.
- Discovery is read-only; cleanup reuses the Cleaner security gate and rollback chain.
- Logs record stage timing, evidence sources, confidence merge, inaccessible items, and deep-scan refusal.
- Tests cover conflicting identity, product-only query, rule collision, corrupt log, stale index, repeated clean, and rollback.
