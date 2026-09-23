# CleanMyPC Next UI Design System

## Principle

Use a calm, information-dense but breathable Windows 11 experience. Every component has semantic states and accessible names; visual polish never hides risk or evidence.

## Tokens

Define background, surface, border, text, accent, success, warning and danger tokens for light and dark themes. Spacing uses a 4px base grid, cards use consistent radius, and all colors meet WCAG contrast targets.

## Components

Buttons, cards, tables, banners, dialogs, progress, empty states and error states share templates. Destructive actions always use danger semantics and an evidence summary.

## Acceptance

Validate theme switching, keyboard focus, high contrast, localization expansion, 100/150/200/300% DPI and UI automation names.

## Implementation dossier

- IDs: `LCN-UI-SYSTEM-001..010`; every token has a semantic name, theme values, contrast target, and deprecation policy.
- Views consume semantic resources only; literal colors, fonts, margins, and shadows are prohibited outside the token layer.
- Each page defines loading, empty, populated, partial, denied, failed, cancelled, and completed visual states.
- Automated checks validate resource resolution; visual review validates hierarchy, contrast, density, and localized expansion.
- Changes include screenshots at required DPI and high-contrast evidence before visual acceptance.
