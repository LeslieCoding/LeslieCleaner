# CleanMyPC Next Typography Standard

## Type scale

Use the platform sans-serif stack with a documented scale for page title, section title, body, secondary text, table rows, metrics and captions. Numeric metrics use tabular figures where comparison matters.

## Layout

Text wraps instead of clipping; minimum sizes support 200% DPI. Long paths and rule evidence use selectable, elided text with a full accessible name.

## Localization

All user-facing strings come from resources. Chinese, English and expansion languages must fit without hardcoded width assumptions.

## Acceptance

Check font fallback, truncation, line height, high contrast, screen reader order and 100–300% DPI screenshots.

## Implementation dossier

- IDs: `LCN-UI-TYPE-001..008`; typography tokens define family fallback, size, weight, line height, and usage semantics.
- User data/path text wraps or truncates with an accessible full-value affordance; no critical error is ellipsized without recovery text.
- Chinese, Latin, symbols, and emoji must fall back predictably without breaking row height or alignment.
- Number, byte, and duration formatting is localized and uses invariant data values underneath.
- Tests capture long publisher names, paths, localized strings, mixed scripts, and high-DPI layouts.
