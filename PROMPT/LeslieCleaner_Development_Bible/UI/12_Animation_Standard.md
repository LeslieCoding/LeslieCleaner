# CleanMyPC Next Animation Standard

## Motion rules

Use short, interruptible transitions for navigation, expansion, selection and progress. Animation communicates state; it must not delay a command or obscure updated data.

## Accessibility

Respect reduced-motion preferences and provide an immediate final state. Loading indicators expose textual progress and never imply completion before verification.

## Performance

Animations run on compositor-friendly properties and do not allocate per frame. Cancellation stops timers and subscriptions when a page is unloaded.

## Acceptance

Test reduced motion, cancellation, rapid navigation, hidden-window cleanup and a 60 FPS budget on supported hardware.

## Implementation dossier

- IDs: `LCN-UI-MOTION-001..008`; animation communicates state transition, never hides a pending destructive operation.
- Motion uses centralized durations/easing tokens and respects system reduced-motion preference.
- Loading indicators are cancellable, do not block keyboard navigation, and have textual status equivalents.
- Navigation cancels or disposes orphaned animations; hidden views cannot retain timers or event subscriptions.
- Performance evidence records frame budget and CPU for result lists, overlays, and page transitions.
