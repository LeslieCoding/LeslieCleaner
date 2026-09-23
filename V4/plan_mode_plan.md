# Project Plan: Implement Plan Mode in TurnManager

## Overview
Implement a "plan mode" for the `TurnManager` class. This mode will allow for a more structured approach to turns, potentially involving multiple steps or a different execution flow when requested.

## Tasks
1. [ ] Define `PlanMode` enum or similar structure to represent the new mode.
2. [ ] Add `planMode` flag/property to `TurnManager`.
3. [ ] Update `TurnManager.execute_turn` (or equivalent) to handle plan mode logic.
4. [ ] Implement step-by-step execution logic when in plan mode.
5. [ ] Add unit tests to verify plan mode functionality.
