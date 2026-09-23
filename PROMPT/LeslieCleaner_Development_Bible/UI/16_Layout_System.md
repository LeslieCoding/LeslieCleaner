# CleanMyPC Next Layout System

## Structure

Every page uses a navigation shell, page header, evidence summary, primary content region and status/feedback region. A 4px spacing grid, responsive columns and minimum window size prevent overlap.

## Responsiveness

Lists virtualize large data sets; cards reflow at narrow widths; dialogs remain usable at 200% DPI. No fixed absolute positioning for dynamic text.

## Acceptance

Test 800px minimum width, narrow window, 100/150/200/300% DPI, long paths, empty/loading/error states and keyboard traversal.

## Implementation dossier

- IDs: `LCN-UI-LAYOUT-001..009`; spacing, grid, radius, and elevation use design tokens rather than page-local constants.
- Pages define responsive breakpoints, minimum usable width, scroll ownership, and large-list virtualization behavior.
- Error/empty/loading content reserves stable space to avoid layout jumps while data refreshes.
- Long paths and localized labels have defined wrapping, tooltip, and copy behavior.
- Screenshot/automation evidence covers narrow window, all required DPI levels, and keyboard traversal order.
