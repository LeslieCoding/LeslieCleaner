# UI Implementation Contract

Version: 2.0  
Status: Normative

Every page and control MUST define loading, empty, populated, partial, denied, failed, cancelled, and completed states. ViewModels own orchestration; views contain presentation and binding only.

Long-running work runs off the dispatcher, reports throttled progress, accepts cancellation, and disables duplicate destructive commands. Collections use stable identity keys and preserve selection when results refresh.

Destructive confirmation shows scope, count, estimated bytes, risk, permission, backup state, and rollback availability. A failure row includes the reason, affected path, required action, and retry or recovery command.

UI acceptance covers keyboard-only navigation, visible focus, 100/125/150/200% DPI, high contrast, screen reader names, localization expansion, and large-result virtualization. Visual inspection and automated contract tests are separate evidence types.
