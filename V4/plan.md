# Task: Refactor `session-control.ts` to support `planMode`

## Background
The current implementation of `session-control.ts` (and related components like `TurnManager`) does not account for a `planMode` where the agent's actions should be more deliberate and follow a pre-defined plan rather than being purely reactive.

## Objective
Introduce a `planMode` flag to the session control logic. When `planMode` is enabled, the behavior of turn management and action execution should shift to prioritize following the current plan.

## Requirements
1.  **Modify `SessionState` / `SessionConfig`**: Add a `planMode: boolean` property.
2.  **Update `TurnManager`**:
    *   Incorporate `planMode` into its decision-making logic.
    *   If `planMode` is active, ensure it prioritizes the next step in the plan.
3.  **Update `session-control.ts`**:
    *   Implement logic to toggle `planMode`.
    *   Ensure `planMode` is correctly propagated through the session state.
4.  **Tests**:
    *   Add unit tests for `session-control.ts` to verify `planMode` behavior.
    *   Add/update tests for `TurnManager` to ensure it respects `planMode`.

## Definition of Done
*   `planMode` flag is present and configurable in the session.
*   `TurnManager` behaves differently when `planMode` is `true` vs `false`.
*   All tests pass.
