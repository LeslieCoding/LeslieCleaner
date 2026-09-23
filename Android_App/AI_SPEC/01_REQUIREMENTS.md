# Android Requirements

- R-001 (USER_EXPLICIT): Build the Android edition in `Android_App` from the Android prompts.
- R-002 (USER_EXPLICIT): Keep Android prompt adaptations in `Android_Prompt` and cover every source file.
- R-003 (AI_RECOMMENDED, safety): Start with a read-only, no-dangerous-permission foundation.
- R-004 (AI_RECOMMENDED): Every future mutation needs typed identity, explicit confirmation, read-back verification, and bounded rollback.

Non-goals for this slice: root cleaning, registry/service/driver emulation, silent deletion, remote AI, device installation, and production release.
