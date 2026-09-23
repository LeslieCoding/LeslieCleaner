# CleanMyPC Next Icon System

## Rules

Icons use a consistent stroke/fill vocabulary and semantic mapping. They supplement labels and never replace accessible text. Product, module, risk and action icons are versioned assets.

## Assets

Prefer optimized vector assets for UI, provide dark/high-contrast variants where needed and keep raster fallbacks for system surfaces. No icon may embed user data or imply a guarantee the operation does not provide.

## Acceptance

Validate resource lookup, missing-icon fallback, contrast, scaling at 300% DPI, automation names and packaging inclusion.

## Implementation dossier

- IDs: `LCN-UI-ICON-001..007`; icons map to semantic actions and states, never ambiguous decorative meaning.
- SVG/vector or DPI-safe assets are preferred; raster fallback has required sizes and transparent background validation.
- Icon-only commands require accessible names, tooltips, focus, disabled state, and danger-state differentiation.
- Missing resource fallback is deterministic and logged during development validation.
- Packaging tests verify all theme and scale resources appear in the installed artifact.
