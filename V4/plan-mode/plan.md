# Plan-mode Plan: Add planMode flag to TurnManager

## Objective
Add a `planMode` flag to `TurnManager` to allow for different execution modes during turn processing.

## Tasks
1. [ ] Define `planMode` property in `TurnManager` class.
2. [ ] Update `TurnManager` constructor to accept `planMode` option.
3. [ ] Implement logic in `TurnManager` to respect `planMode`.
4. [ ] Update existing callers of `TurnManager` to provide `planMode` or handle its absence.
5. [ ] Add tests for `planMode` functionality.
