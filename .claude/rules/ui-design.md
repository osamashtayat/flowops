---
paths:
  - "src/frontend/src/**/*.tsx"
  - "src/frontend/src/styles/**/*.css"
---

# UI design rules

Source of truth: `docs/DESIGN_SYSTEM.md` and `src/frontend/src/styles/theme.css`.

## Identity
- Warm, calm, minimal: ivory canvas (`bg-canvas`), sand sidebar (`bg-sidebar`), off-white cards (`bg-surface`), one muted sage accent (`primary`).
- **No orange-led or black-led styling**, no neon, no gradients, no heavy shadows, no dark mode in Phase 1.

## Tokens only
- Colours, radii, shadows, fonts only via theme tokens (`bg-surface`, `text-ink-muted`, `border-border-strong`, `rounded-md`, `shadow-card`…). Tailwind's default palette is disabled on purpose.
- Forbidden in components: hex/rgb/hsl literals, arbitrary colour classes (`bg-[#…]`), inline `style` colours. `npm run lint:tokens` enforces this.
- New token? Add it to `theme.css` **and** `DESIGN_SYSTEM.md` with its contrast ratio; never ad hoc.

## Components
- Use `components/ui` primitives; don't restyle them per page (variant props exist for that). If you need a new variant, add it to the component.
- Status is always label + icon + tone via `StatusBadge` and `lib/format/status.ts`, never colour alone.
- One primary button per region; destructive and decision actions use `ConfirmDialog`/`DecisionDialog` with an action-named confirm button.

## Accessibility (WCAG 2.2 AA)
- Text contrast ≥ 4.5:1 (use `ink`/`ink-muted` on approved surfaces only); control boundaries ≥ 3:1 (`border-border-strong` on inputs — `border-border` is decorative only).
- Visible focus: `focus-visible:outline-2 outline-offset-2 outline-primary`. Never remove focus styles.
- Targets ≥ 24×24 px; respect `prefers-reduced-motion`.

## Layout & typography
- Body text `text-base` (15 px), tables/labels `text-sm`, `tabular-nums` for numbers, money, ids and dates.
- Page = `PageHeader` + cards on canvas; content max width 1200 px; gutters 16 px (mobile) / 32 px (desktop).
- Responsive: sidebar drawer < 1024 px; tables stack < 640 px; dialogs become sheets < 640 px.
- Every async view designs loading (skeleton), empty and error states.
