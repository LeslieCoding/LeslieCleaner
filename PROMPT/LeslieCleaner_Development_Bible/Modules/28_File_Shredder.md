# CleanMyPC Next File Shredder

## Objective

Provide explicit, honest secure-delete workflows with a safe fallback for SSDs.

## Modes

Support single files, folders and queued jobs. Overwrite pass counts 3, 7 and 35 are available only where the filesystem permits; SSD and copy-on-write volumes use the documented device trim limitation instead of claiming guaranteed physical erasure.

## Safety

Require confirmation, reject protected paths and reparse escapes, report inaccessible items separately and support cancellation at chunk boundaries. Never shred backup or quarantine data without an additional confirmation.

## Acceptance

Test queue ordering, cancellation, locked files, protected paths, sparse files, reparse points, pass counts and truthful result reporting.

## Implementation dossier

- IDs: `LCN-SHRED-FILE-001..009`; inputs are canonical selections, overwrite policy, pass count, volume type, and explicit confirmation.
- Output includes per-item overwrite/delete result, verification, SSD caveat, and irreversibility acknowledgement.
- Protected/system files and reparse points are refused before any overwrite is attempted.
- Locked, sparse, encrypted, compressed, or unsupported resources produce typed outcomes, never a false success.
- Logs record volume, algorithm, passes, attempted bytes, verification state, and confirmation evidence.
- Tests cover files, directories, locks, reparse points, cancellation, partial failure, and unsupported volumes.
