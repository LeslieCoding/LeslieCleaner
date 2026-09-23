# UX Implementation Contract

Version: 2.0  
Status: Normative

The product must make system risk understandable before action. Every workflow states what was found, why it is associated with the selected application, what will change, what is protected, and how to undo it.

Batch actions use a preview-confirm-execute sequence and show per-item outcomes. History records operation ID, timestamp, scope, result, backup, rollback, and failure details. Notifications are actionable and deduplicated; they never hide a failure behind a success toast.

Search ranks exact identity above prefix and contains matches, normalizes Unicode and spacing, explains why a result matched, and never searches outside the approved scope. AI recommendations are optional, evidence-linked, redacted, and never the authority for execution.

UX acceptance includes cancellation, retry, back navigation, stale result protection, offline mode, accessibility, localization, and recovery after an elevated helper returns.
