# CleanMyPC Next Color System

## Semantic palette

Colors represent meaning, not decoration: neutral for information, blue for primary action, green for verified success, amber for caution and red for destructive/error. Never use color as the only status channel.

## Themes

Light, dark and high-contrast themes map the same semantic tokens. Contrast is measured for text, icons, borders, disabled controls and focus indicators.

## Safety

Risk levels and permission states include text/icon labels. Red is reserved for irreversible or dangerous consequences.

## Acceptance

Automated token checks and manual contrast review cover all pages, disabled states, charts and localized strings.

## Implementation dossier

- IDs: `LCN-UI-COLOR-001..009`; colors are semantic (`Surface`, `Text`, `Warning`, `Danger`) rather than component-specific literals.
- Light, dark, and high-contrast themes define every semantic token and chart palette counterpart.
- Risk is never encoded by color alone; text, icon, and automation description provide redundant meaning.
- Contrast targets are documented per state, including disabled controls and selected rows.
- Theme tests fail when a token is missing, circular, or maps to an unreadable fallback.
