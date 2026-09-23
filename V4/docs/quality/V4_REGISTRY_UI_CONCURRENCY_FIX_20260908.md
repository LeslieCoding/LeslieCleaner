# V4 Registry/UI Reliability Fix - 2026-09-08

## Finding

`RegistryViewModel.FixAsync` accepted a second command invocation while the first batch was still
waiting for confirmation or executing. `CleanEngine` serialized its own operation gate, but that
did not protect the mutable UI selection. A second invocation could therefore submit stale rows,
create a different backup, and make a successful first deletion look like a repeated failure.

## Fix

The command now returns immediately when `IsBusy` is already true, before showing confirmation or
capturing the selection. This preserves one authoritative backup/delete/read-back transaction per
UI batch. The behavior is also pinned by the UI acceptance contract test.

## Verification

- Registry and UI focused tests: 90/90 passed.
- Full non-privileged V4 regression: 471/471 passed on the clean rerun.
- The first parallel full run exposed a separate test-harness race in global Serilog ownership;
  the isolated logging test and the subsequent clean full run passed. It is tracked as a logging
  hardening item rather than counted as a product failure.

## USB preparation

`scripts/Test-UsbDebug.ps1 -AsJson` ran read-only on 2026-09-08 and reported `DebugToolsMissing`:
no `adb`/`fastboot` was installed and no debug-capable board was attached. Windows USB enumeration
itself worked. The script made no device or host mutation.

## Scanner accuracy follow-up

The registry scanner now collapses duplicate rule hits by physical `(KeyPath, ValueName)` identity,
case-insensitively, while retaining the highest-risk finding. This prevents the UI from showing two
rows for one target and prevents a lower-risk duplicate from winning when the cleaner groups a batch.
Physical 32-bit/64-bit roots remain distinct (`HKLM32` versus `HKLM`), so view-specific deletion and
read-back are not merged accidentally. Registry scanner/engine focused coverage is 20/20 and the
  full non-privileged gate remains green at 472/472 after the alias regression test.

Backup matching now canonicalizes equivalent hive aliases (`HKCU64`, `HKCU`, and
`HKEY_CURRENT_USER`, while preserving 32-bit versus 64-bit views). A valid backup can no longer be
rejected merely because the caller used a different textual root alias, and a backup from the wrong
physical view still cannot match.

The human-readable log sink is now synchronous (`buffered: false`, shared file access); destructive
events are therefore written before their operation returns rather than waiting on an async worker.
