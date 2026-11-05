---
name: review-ui
description: Review FlowOps frontend pages or components against the beige design system — tokens, shared components, accessibility, typography, spacing, usability, responsive layouts, and loading/error/empty states. Use after UI work or when asked to audit a screen.
argument-hint: "[paths or route, default: changed frontend files]"
---

Review the UI in: $ARGUMENTS (if empty, the frontend files changed versus `main`).

Reference: `docs/DESIGN_SYSTEM.md`, `docs/UI_SPEC.md`, `.claude/rules/ui-design.md`, `src/frontend/src/styles/theme.css`.

## Automated checks (run first, report output)
```bash
cd src/frontend && npm run lint:tokens && npm run lint && npm run typecheck
```

## Checklist
1. **Tokens** — only theme classes; no hex/rgb, arbitrary colour classes, inline colour styles; no orange/black-led styling; `border-border-strong` on control outlines, `border-border` only decorative.
2. **Shared components** — Button, FormField/Input/Select, DataTable, Pagination, Dialog/ConfirmDialog/DecisionDialog, StatusBadge, EmptyState/ErrorState/Skeleton, PageHeader, Card used instead of page-local markup. Any duplicated pattern → name the component to extract/reuse.
3. **Typography & hierarchy** — one `h1` (page title, `text-xl`); body `text-base`; tables `text-sm`; `tabular-nums` for numbers/money/ids/dates; ink-muted only for secondary info.
4. **Spacing & layout** — 4 px scale; consistent card padding (`p-5`); max-width 1200; gutters; alignment of form fields and table columns (numbers right-aligned).
5. **States** — loading (skeleton matching layout), empty (helpful sentence + action), error (mapped message + retry), success feedback after actions, 409 handling for decisions.
6. **Accessibility** — contrast pairs from DESIGN_SYSTEM §1.4; visible focus everywhere; keyboard path through the page and dialogs (focus trap/return, Esc); labels and `aria-describedby` on inputs; status not colour-only; icons labelled or `aria-hidden`; targets ≥ 24 px; landmarks; reduced motion.
7. **Usability** — can an untrained employee complete the task? Is the primary action obvious? Are destructive/decision actions confirmed with action-named buttons? Are filters/page in the URL?
8. **Responsive** — reason about 360, 768, 1280 px: sidebar drawer < 1024, stacked tables < 640, dialogs as sheets < 640, no horizontal scroll on content.
9. **Spec match** — fields and behaviour match `docs/UI_SPEC.md` and real contract fields (nothing invented).

## Output
Per finding: **Blocker / Major / Minor** · `path:line` · user impact · fix with existing tokens/components. Then "Keep" — patterns worth reusing. Recommend the smallest fix; never a restyle away from the design system.
