# Frontend run — 2026-11-06

Autonomous session on one worktree/branch (`task/FE-run-2026-11-06`), one squash-merged PR per step, from the latest `main` each time: real auth in dev, then FE-07, FE-08, FE-09, FE-12. All five are merged. Everything is verified against **MSW mocks** except the auth flows, which were exercised against the real API.

## Needs your decision

1. **Backend question that will reach the admin screens (FE-13):** `docs/AGENT_HANDOFF.md` §6 now holds an open item from BE-08's security review — may administrators give themselves process authority (self-role, self-manager, password reset)? It arrived while I was working (merged as #16) and does not affect FE-07…FE-12. FE-13 will need the answer (a new 422 code if self-changes are forbidden).
2. **Leave length limit — resolved: you confirmed inclusive counting.** BE-10 should count the same way. The contract says "≤ 90 days" and DOMAIN_MODEL says "span ≤ 90 calendar days". I implemented it as 90 calendar days *inclusive* of both ends (the same count as the "N calendar days" hint and `LeaveDetailsDto.days`), so 1 Jan–31 Mar is accepted and 1 Jan–1 Apr is not. If the backend counts `end − start` instead, the client is one day stricter; the server stays the authority. Please confirm with BE-10.
3. **Local dev data changed.** To test the real login I changed the bootstrap admin's password and changed it back. Net effect on your local database: `admin@flowops.local` no longer has `mustChangePassword = true`. Another API instance (not mine) was already listening on `https://localhost:7180`, so I ran mine on `:7181` and stopped it afterwards.

## Step 0 — Real auth API in dev

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/11 (merged)
- **What:** login, logout, `/auth/me` and change-password go to the real API in the dev browser. MSW keeps only data endpoints without a merged backend task (`dataHandlers`); mocked handlers find out who is calling through the real `/api/auth/me` (`mocks/actor.ts`). Tests keep the in-memory mock session. The dev "mock user" switcher is removed. The XSRF cookie/header names were confirmed against `AuthorizationSetup.cs` (`XSRF-TOKEN` / `X-XSRF-TOKEN`).
- **Real-API check (manual, browser pane → Vite dev server → API on :7181):** login with the bootstrap admin → redirected to `/change-password` (must-change) · change password → 204 with the XSRF header, then `/me` refetched, shell shown · logout from the user menu → `/login`, `GET /api/auth/me` → 401 · password restored via the API (200 / 204 / 204).
- **Decisions:** `src/mocks/**` is exempt from the "no global `fetch`" lint rule (mocks need `fetch(bypass(...))`); no other code is.
- `/review-ui`: not run (no screen changed).

## Mid-run: handoff §6 item from the backend

BE-05 follow-up (#12) landed during the run and added an open item: 423 / `auth.locked_out` gone, `auth.rate_limited` (429) new, login 401 message to mention the pause. Done in the FE-08 PR (separate commit): `problem.ts`, mocks (`MOCK_RATE_LIMITED_EMAIL`), tests, UI_SPEC §4.1, types regenerated, §6 row removed.

## FE-07 — New request flow

- **Status:** Done (mock-integrated; BE-10 not merged) · **PR:** https://github.com/osamashtayat/flowops/pull/13
- **Checks (src/frontend):** `npm run typecheck` ok · `npm run lint` ok (oxlint + "Design-token check passed.") · `npm test` 354 passed / 0 failed (23 files) · `npm run build` ok.
- **What:** `/requests/new` type picker (two links), `/requests/new/purchase` and `/leave` forms with zod schemas mirroring the contract, server 400 → field, 422 → banner, no manager/department → blocking notice and disabled form, success toast + navigate to `/requests/:id`. New shared `Alert`, `RequestFormFrame` (shared by both forms), `useSubmitOutcome`; `calendarDaysInclusive` / `todayInCompanyZone` in `lib/format/date.ts`.
- **Decisions:** leave type defaults to "Annual leave" (a valid value) instead of an empty choice; the client also rejects a start date before today in Amman, as the contract says.
- **`/review-ui`:** no Blockers/Majors. Minor, not fixed: `Alert` carries no icon (the message text is the cue); `LoginPage`/`ChangePasswordPage` still use their own inline banner markup — switch them to `Alert` (follow-up).

## FE-08 — My requests & Team requests

- **Status:** Done (mock-integrated; BE-11 not merged) · **PR:** https://github.com/osamashtayat/flowops/pull/14 (includes the §6 commit)
- **Checks:** typecheck ok · lint ok · `npm test` 369 passed / 0 failed (24 files) · build ok.
- **What:** `RequestsList` (status/type/search with 300 ms debounce, DataTable with all states, Pagination) shared by both pages; everything in the URL; URL values the contract does not know are ignored; "page past the end" message. `requestStageLabel` added to `lib/format/status.ts`.
- **`/review-ui`:** no Blockers/Majors; one Minor fixed (past-the-end page said "no requests").

## FE-09 — Request details

- **Status:** Done (mock-integrated; BE-11/12 not merged) · **PR:** https://github.com/osamashtayat/flowops/pull/15
- **Checks:** typecheck ok · lint ok · `npm test` 394 passed / 0 failed (25 files) · build ok.
- **What:** header with status + "Waiting for: …", details (leave reason "Restricted" when `reasonHidden`), approval steps, summary, actions **only from `availableActions`**, `ActivityTimeline` (shared), comment box, `DecisionDialog` + `DecisionActions` + `useDecideTask` (sends `version`; 409 → mapped toast, dialog closes, queries refetched; other errors stay in the dialog with the typed text; reject needs a reason). 404 → "doesn't exist or you don't have access".
- **Decisions:** `REQUEST_LIMITS` moved from `features/requests/limits.ts` to `lib/api/limits.ts` so `features/tasks` and `features/requests` don't import each other (CODE_STANDARDS and `.claude/rules/frontend.md` updated). The mock imitates the fixed processes coarsely (`mocks/requestWorkflow.ts`); the real order and eligibility are the backend's.
- **`/review-ui`:** no Blockers/Majors. Fixed: specific decision toasts, an `h1` on the error state. **Minor, not fixed:** below 1024 px the Actions card sits after Details and Steps, so a decider scrolls to it (the header already says what the request waits for).

## FE-12 — Dashboard

- **Status:** Done (mock-integrated; BE-15 not merged) · **PR:** https://github.com/osamashtayat/flowops/pull/17
- **Checks:** typecheck ok · lint ok · `npm test` 403 passed / 0 failed (26 files) · build ok.
- **What:** tasks card, status tiles (link to `/requests?status=…`), recent requests, recent activity (request numbers linked), admin card (warning tone + text when stalled > 0); skeleton, page-level error with retry, empty state. Sections the server sends as `null` are not shown. People who cannot submit get no personal-request section. `FoundationPage`, `DataDisplayDemo`, `OverlaysDemo` removed (they were review aids "removed with FoundationPage"); `docs/AGENT_HANDOFF.md` §7 still mentions the FoundationPage in a historical prompt.
- **`/review-ui`:** no Blockers/Majors. Minors: link-card markup repeats in `RequestStatTiles`, `AdminCard` and `NewRequestPage` (extract a `LinkCard`); the recent-requests columns duplicate those of `RequestsList` (share the column definitions).

## Final check on `main` (86f1885 + this report)

`npm run typecheck` exit 0 · `npm run lint` exit 0 ("Design-token check passed.") · `npm test` 26 files / 403 tests passed · `npm run build` exit 0. `npm run api:types` after BE-08 (#16) changes `schema.d.ts` (admin endpoints); committed with this report. Nothing uses them yet.

## Not done / not verified

- No UI was looked at in a browser for FE-07…FE-12, and none at 360/768/1280 px; layout was reasoned from the code. Only the auth flows ran against the real API.
- The `/review-ui` passes were done by me reading the code against the skill's checklist, not by a separate reviewer agent.
- Real-API integration for requests, tasks and the dashboard is untested; the request/decision/dashboard handlers are mocks written from the contract. Re-test when BE-10, BE-11, BE-12 and BE-15 land (watch the generated client's capitalised query names `Scope`, `Status`, `PageSize`).
- Remaining Minors listed above are follow-ups, not blockers: `Alert` in the session pages, `LinkCard`, shared request columns, Actions card position on small screens.
