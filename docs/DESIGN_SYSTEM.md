# Design System — FlowOps

**Purpose:** the visual language of FlowOps: tokens, components, states and accessibility rules.
**Implementation source of truth:** `src/frontend/src/styles/theme.css` (Tailwind v4 `@theme`). If this document and `theme.css` disagree, fix one of them in the same change.

Character: **warm, calm, legible**. A paper-like ivory canvas, a soft sand sidebar, a single muted sage accent. It should feel like a well-made notebook, not a cockpit. No orange accents, no black chrome, no gradients, no glassmorphism, no neon.

---

## 1. Colour tokens

All contrast ratios below were computed (WCAG 2.x relative luminance) — see the table in §1.4.

### 1.1 Surfaces and text

| Token (`--color-*`) | Hex | Tailwind class examples | Use |
|---|---|---|---|
| `canvas` | `#F7F4EF` | `bg-canvas` | App background |
| `sidebar` | `#EEE9E1` | `bg-sidebar` | Sidebar background |
| `sidebar-active` | `#E3DCD2` | `bg-sidebar-active` | Active nav item |
| `surface` | `#FFFCF8` | `bg-surface` | Cards, tables, dialogs, inputs |
| `surface-muted` | `#F1ECE5` | `bg-surface-muted` | Table header, secondary panels, hover rows |
| `border` | `#DAD3CA` | `border-border` | Dividers and card outlines (**decorative only**) |
| `border-strong` | `#8C8378` | `border-border-strong` | Input, select, checkbox outlines (needs 3:1) |
| `ink` | `#343B36` | `text-ink` | Primary text |
| `ink-muted` | `#646A63` | `text-ink-muted` | Secondary text, labels, metadata |
| `primary` | `#526B5C` | `bg-primary`, `text-primary` | Primary actions, links, focus ring |
| `primary-hover` | `#405747` | `hover:bg-primary-hover` | Hover/pressed primary |
| `primary-soft` | `#E4EBE5` | `bg-primary-soft` | Selected rows, active filter chips |
| `on-primary` | `#FFFCF8` | `text-on-primary` | Text on primary/danger fills |

### 1.2 Semantic (status) colours — always as *pairs*

| Token pair | Text/icon | Background | Used for |
|---|---|---|---|
| `info` / `info-soft` | `#3D5A73` | `#E2E9EF` | Approved (awaiting fulfillment), informational notices |
| `success` / `success-soft` | `#2F6B45` | `#E3EFE5` | Completed, success toasts |
| `warning` / `warning-soft` | `#8A5A12` | `#F6ECD6` | Pending review, open tasks, stalled warnings |
| `danger` / `danger-soft` | `#A1392F` | `#F7E3DF` | Rejected, errors, destructive actions |
| `neutral` / `neutral-soft` | `#5B5F57` | `#ECE8E0` | Done tasks, inactive, closed metadata |
| `danger-hover` | `#87302A` | — | Hover on solid danger button |

The warning hue is a deep ochre-brown used only in small badges and banners, never as a fill for large areas or buttons. That keeps it distinguishable without an orange-heavy theme.

### 1.3 Forbidden
- Tailwind's default palette is **disabled** (`--color-*: initial` in `theme.css`), so `bg-orange-500`, `text-black` etc. do not compile to anything.
- Arbitrary colour values (`bg-[#…]`, `text-[rgb(…)]`, inline `style={{ color }}`) are rejected by `npm run lint:tokens`.
- Pure black `#000` and pure white `#FFF` as large surfaces.

### 1.4 Verified contrast

| Foreground on background | Ratio | Requirement |
|---|---|---|
| ink on canvas / sidebar / surface | 10.5 / 9.5 / ≥10 | 4.5 ✔ |
| ink-muted on canvas / sidebar / surface-muted / surface | 5.1 / 4.6 / 4.7 / 5.4 | 4.5 ✔ |
| on-primary on primary / primary-hover | 5.7 / 7.7 | 4.5 ✔ |
| primary (as link text) on canvas | 5.3 | 4.5 ✔ |
| border-strong (input outline) on surface | 3.6 | 3.0 (non-text) ✔ |
| primary (focus ring) on surface | 5.7 | 3.0 ✔ |
| info / success / warning / danger / neutral text on own soft bg | 5.9 / 5.4 / 5.0 / 5.4 / 5.3 | 4.5 ✔ |
| on-primary on danger | 6.5 | 4.5 ✔ |
| ink / ink-muted on primary-soft (selected rows, unread) | 9.5 / 4.6 | 4.5 ✔ |
| `border` (#DAD3CA) on canvas | 1.35 | decorative only — **never** the sole boundary of an interactive control |

---

## 2. Typography

Font: **Public Sans** (variable, self-hosted via `@fontsource-variable/public-sans`; no external font CDN). Neutral, highly legible at small sizes, slightly warmer than system UI fonts. Fallback: `ui-sans-serif, system-ui, sans-serif`.

| Token | Size / line-height | Weight | Use |
|---|---|---|---|
| `text-xs` | 12 / 16 | 500 | Badges, table meta, helper text |
| `text-sm` | 14 / 20 | 400–500 | Table cells, labels, buttons |
| `text-base` | 15 / 24 | 400 | Body text, inputs (comfortable for long sessions) |
| `text-lg` | 17 / 26 | 600 | Card titles, dialog titles |
| `text-xl` | 20 / 28 | 600 | Page titles |
| `text-2xl` | 24 / 32 | 600 | Dashboard numbers |

- Numbers, money, request numbers and dates use `tabular-nums`.
- Max line length for prose ≈ 70ch.
- No all-caps body text; section labels may use `text-xs uppercase tracking-wide text-ink-muted`.

## 3. Spacing, radius, borders, shadows

- **Spacing:** Tailwind's 4 px scale; use 1, 1.5, 2, 3, 4, 6, 8, 12 for component internals and 6/8 between page sections. Page gutter: 16 px mobile, 32 px desktop.
- **Radius:** `rounded-sm` 6 px (inputs, buttons, badges' inner elements), `rounded-md` 10 px (cards, tables, menus), `rounded-lg` 14 px (dialogs), `rounded-full` (status badges, avatar).
- **Borders:** 1 px. Cards: `border-border`. Controls: `border-border-strong`.
- **Shadows:** `shadow-card` (`0 1px 2px rgb(52 59 54 / 0.06)`) on cards; `shadow-overlay` (`0 8px 24px rgb(52 59 54 / 0.12)`) on menus/dialogs only. Nothing heavier.

## 4. Layout

```
┌───────────────┬──────────────────────────────────────────────┐
│  FlowOps      │  TopBar: page context · 🔔 (count) · user ▾  │ 56 px
│  (sidebar     ├──────────────────────────────────────────────┤
│   248 px,     │  PageHeader: title, subtitle, primary action │
│   bg-sidebar) │                                              │
│  Dashboard    │  Content (max-w 1200, centered, px-8)        │
│  My requests  │  Cards on bg-surface over bg-canvas          │
│  New request  │                                              │
│  Inbox (3)    │                                              │
│  Admin ▸      │                                              │
└───────────────┴──────────────────────────────────────────────┘
```
- ≥ 1024 px: fixed sidebar. < 1024 px: sidebar becomes an off-canvas drawer opened from a menu button in the top bar.
- Tables become stacked "row cards" below 640 px (each row a small card with label/value pairs).
- Dialogs: max-w 480 (confirm) / 640 (forms); full-screen sheet below 640 px.

## 5. Components (appearance + states)

Every component below lives once in `src/components/ui/` (or `layout/`). Pages compose them; they do not restyle them.

### Button — `variant: primary | secondary | ghost | danger`, `size: sm | md`
| Variant | Rest | Hover | Disabled |
|---|---|---|---|
| primary | `bg-primary text-on-primary` | `bg-primary-hover` | 50 % opacity, `cursor-not-allowed` |
| secondary | `bg-surface text-ink border border-border-strong` | `bg-surface-muted` | same |
| ghost | transparent, `text-ink` | `bg-surface-muted` | same |
| danger | `bg-danger text-on-primary` | `bg-danger-hover` | same |
Height 36 (sm 32); `px-4`; `rounded-sm`; `text-sm font-medium`. `loading` prop shows a spinner, keeps width, sets `aria-busy`. One primary button per view region.

### Focus (all interactive elements)
`focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary`. Never remove outlines without this replacement.

### Inputs (Input, Textarea, Select, DateInput)
`bg-surface border border-border-strong rounded-sm px-3 h-10 text-base text-ink`, placeholder `text-ink-muted`. Invalid: `border-danger` + message below in `text-danger text-sm` linked via `aria-describedby`; `aria-invalid="true"`. Read-only: `bg-surface-muted`, no border change.

### FormField
Label (`text-sm font-medium text-ink`, required marker "*" with `aria-hidden` + `required` on input), control, helper text (`text-xs text-ink-muted`), error. Vertical stack, `gap-1.5`; fields separated by `gap-5`.

### StatusBadge
Pill: `rounded-full px-2.5 py-0.5 text-xs font-medium` + icon (12 px) + label. Colour from the semantic pair; label always present (never colour alone).

| Domain value | Label | Tone | Icon |
|---|---|---|---|
| Request `InReview` | Pending review | warning | clock |
| Request `Approved` | Approved | info | check |
| Request `Completed` | Completed | success | check-circle |
| Request `Rejected` | Rejected | danger | x-circle |
| Task `Open` | Awaiting action | warning | clock |
| Task `Done` + outcome | Approved / Rejected / Fulfilled | success / danger / success | as above |

Mapping lives in `lib/format/status.ts`, not in components.

### Card
`bg-surface border border-border rounded-md shadow-card`; header `px-5 py-4 border-b border-border`; body `p-5`.

### DataTable
Header row `bg-surface-muted text-xs font-medium text-ink-muted`; rows 48 px, `border-b border-border`, hover `bg-surface-muted`; clickable rows are real links (`<a>` in the first cell) so they work with keyboard and middle-click. Right-align numbers. Built-in loading (skeleton rows), empty and error slots.

### Pagination
"Showing 21–40 of 132" + Previous/Next + page size select. Page state in the URL (`?page=2`).

### Dialog / ConfirmDialog
Native-like modal: focus trapped, `Esc` closes, focus returns to trigger, `aria-labelledby` title. ConfirmDialog for decisions: title states the action and object ("Reject FO-2026-000123?"), body summarises consequence, reason textarea when required, primary button names the action ("Reject request"), never "OK".

### Toast
Bottom-right stack, auto-dismiss 5 s (errors persist until dismissed), `role="status"` (success) / `role="alert"` (error).

### ConnectionBanner
Sticky strip above the whole app while the server can't be reached (UI_SPEC §2.1): `bg-warning-soft text-warning text-sm`, `WifiOff` icon (`aria-hidden`), one sentence saying what happened and what it means, and a secondary `sm` **Try again** button (spinner while checking). Centred, wraps on small screens. Its container is an always-mounted `aria-live="polite"` region so the message is announced when it appears. Never a toast: one persistent message instead of one per failed request.

### EmptyState / ErrorState / Skeleton
Empty: short sentence + optional action ("You have no requests yet. — New request"). Error: message from `problem.ts` + Retry. Skeleton: `bg-surface-muted animate-pulse rounded-sm` blocks matching final layout; respect `prefers-reduced-motion`.

### ActivityTimeline
Vertical list, 24 px icon column with a 1 px `border` line; entry = icon (tone by type) · **actor** action · relative time (absolute on hover/title) · optional quoted body in `bg-surface-muted rounded-sm p-3`.

### Navigation
Sidebar items 40 px, `rounded-sm`, `text-sm font-medium text-ink`; active `bg-sidebar-active` + `aria-current="page"`; count badge on Inbox (`bg-primary text-on-primary text-xs rounded-full`).

## 6. Interaction principles
- Every async action shows progress within 100 ms (button spinner) and an outcome (toast or inline).
- Destructive or irreversible actions (reject, approve, fulfill, deactivate user) always confirm.
- Optimistic updates **only** for marking notifications read. Decisions wait for the server.
- Errors say what happened and what to do ("Someone else already handled this task. The inbox has been refreshed.").
- Dates: "5 Nov 2026" in tables; relative ("3 h ago") in timelines with absolute in `title`.

## 7. Accessibility checklist
- WCAG AA contrast (§1.4); controls' boundaries ≥ 3:1.
- All functionality reachable by keyboard; visible focus everywhere; logical tab order; skip-to-content link.
- Landmarks: `nav`, `main`, `header`; one `h1` per page (the page title).
- Form errors announced (`aria-live="polite"` summary on submit) and linked to fields.
- Status never conveyed by colour alone (labels + icons).
- Icons that convey meaning have `aria-label`; decorative icons `aria-hidden`.
- Respect `prefers-reduced-motion`.
- Target size ≥ 24×24 px (WCAG 2.2 2.5.8).

## 8. Out of scope (Phase 1)
Dark mode, theme switching, user-customisable colours, illustration sets, charts beyond simple counts.
