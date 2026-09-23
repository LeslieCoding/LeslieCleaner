# Task: Implement Plan Mode

Implement a "plan mode" that allows users to generate a plan for a task before executing it.

## Requirements

1.  **Plan Mode Flag**: Add a `planMode` flag to the `TurnManager` or a similar central component to track if plan mode is enabled.
2.  **Plan Generation**: When `planMode` is enabled, the agent should generate a detailed step-by-step plan for the user's request instead of executing it directly.
3.  **User Confirmation**: After generating a plan, the agent must wait for user confirmation before proceeding with any actions.
4.  **Execution**: If the user confirms the plan, the agent should proceed to execute the steps in the plan.
5.  **UI/UX**: (If applicable) Provide a clear visual indication that plan mode is active.

## Implementation Steps

1.  [ ] Add `planMode` flag to `TurnManager`.
2.  [ ] Update `TurnManager` logic to detect `planMode` and switch to plan generation mode.
3.  [ ] Implement plan generation prompt/logic.
4.  [ ] Implement user confirmation step in the interaction loop.
5.  [ ] Verify that execution proceeds only after confirmation when `planMode` is on.
