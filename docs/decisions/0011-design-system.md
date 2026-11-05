# ADR-0011: Token-first Tailwind v4 design system with disabled default palette

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
The UI must be consistently warm/beige, accessible, and resistant to drift as several agents add pages.

## Decision
All design tokens are defined once in `src/frontend/src/styles/theme.css` using Tailwind v4 `@theme`, with the default colour palette removed (`--color-*: initial`) so only FlowOps colours exist. A lint script rejects hex/rgb literals and arbitrary colour classes in components. Shared components in `components/ui` own styling; pages compose them. Contrast pairs are documented and verified in DESIGN_SYSTEM.md. Light theme only.

## Consequences
+ Off-palette colours are impossible by construction, not by review.
+ Re-theming means editing one file.
- Developers can't reach for quick Tailwind colours (intended).
- Tailwind v4 CSS-first config differs from v3 tutorials; agents must follow the docs, not v3 habits.

## Alternatives rejected
Component library like MUI/Chakra (fights the custom aesthetic, heavier); CSS modules per component (tokens harder to enforce); shadcn/ui copy-in components wholesale (fine to borrow patterns from, but not imported en masse).
