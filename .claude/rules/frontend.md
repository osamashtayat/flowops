---
paths:
  - "src/frontend/**/*.ts"
  - "src/frontend/**/*.tsx"
---

# Frontend rules (React + TypeScript)

Read with: `docs/UI_SPEC.md`, `docs/DESIGN_SYSTEM.md`, ADR-0008. Visual rules: `ui-design.md`.

## Structure
- `app/` setup & routing · `components/ui` design-system primitives · `components/layout` shell · `features/<feature>/{api.ts, components/, pages/, schemas.ts}` · `lib/{api,realtime,format}`.
- Features may import from `components/*` and `lib/*`; avoid importing another feature's internals — promote shared pieces to `components/` or `lib/`.
- Named exports; one component per file; `@/` alias for `src/`.

## Data
- All HTTP goes through `lib/api/client.ts` (openapi-fetch + generated `schema.d.ts`). No `fetch`/`axios` elsewhere. Never hand-edit or hand-write API types.
- Server state only via TanStack Query hooks defined in `features/*/api.ts`, keys from `lib/api/queryKeys.ts`. No `useEffect` data fetching. No server data copied into `useState`/Context.
- Mutations invalidate the keys listed in `docs/UI_SPEC.md` §6 / `API_CONTRACT.md` §9. Optimistic updates only for marking notifications read.
- Decisions always send `version`; handle 409 by showing the mapped message and invalidating.
- Errors: render `toUserMessage(error)` from `lib/api/problem.ts`. Map 400 `errors` to form fields.

## Components
- Pages compose; they don't contain fetch orchestration, permission algorithms, or formatting logic.
- Use existing primitives (Button, FormField, DataTable, Dialog, StatusBadge, EmptyState…) before writing markup. Repeating a pattern twice → make it shared.
- Every data view handles loading, empty, and error states explicitly.
- Permissions: `useCapability()` and server-provided `availableActions` only.
- Filters/page/tab state in the URL (`useUrlState`).

## Forms
- react-hook-form + zod schemas in `features/*/schemas.ts`; limits imported from `lib/api/limits.ts` (mirrors API_CONTRACT).
- Labels, `aria-describedby` for help/error, `aria-invalid`, focus first invalid field on submit.

## TypeScript
- `strict` on; no `any` (use `unknown` + narrowing); no non-null `!` without a comment; exhaustive `switch` with `satisfies never` on unions (status, enums).
- No `dangerouslySetInnerHTML`.

## Accessibility
- Semantic elements first (`button`, `a`, `nav`, `main`, `table`); keyboard operable; visible focus; one `h1` per page; icons with labels or `aria-hidden`.
