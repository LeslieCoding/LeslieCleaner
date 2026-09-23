# CleanMyPC Next Registry Cleaner Module

## Objective

Find high-confidence registry residue without traversing arbitrary hives and perform only backed-up, verified mutations.

## Scan rules

Rules target invalid extensions, COM/CLSID references, uninstall residue, fonts, ActiveX and known application remnants. Each result includes hive, 32/64-bit view, key/value identity, evidence, risk score, confidence and protection state. Physical `HKCU/HKLM\SOFTWARE\Classes` views are used for class scans; merged views are not treated as physical evidence.

## Mutation contract

Require a matching backup, security-gate approval, explicit confirmation and post-delete readback. On failure, classify elevation/protection/verification and attempt verified restoration.

## Acceptance

Cover 32/64-bit views, case-insensitive value names, missing targets, protected keys, repeated deletion, backup corruption, cancellation and restoration.

## Implementation dossier

- IDs: `LCN-REG-RULE-001..012`; inputs name hive, physical view, key/value identity, evidence, and precondition.
- Pipeline: open physical view → pattern match → protected policy → backup → delete → flush → read-back.
- Output includes risk, protected status, original value casing, backup hash, and rollback ID.
- HKLM/WRP/integrity targets require elevation; HKCU remains view-explicit.
- Logs include hive, view, path, value kind/name, permission decision, and verification result.
- Tests cover 32/64 separation, repeated deletion, case-insensitive names, restore, protected refusal, and denied ACL.
