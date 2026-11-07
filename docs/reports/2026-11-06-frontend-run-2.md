# Frontend run 2 — 2026-11-06

Three tasks, each on its own branch and worktree from the latest `main`, each squash-merged by PR after typecheck, lint, test and build were green (AGENTS.md §4): FE-13, FE-10, then the real-API switch. Unit tests run on MSW mocks; the real-API step was exercised by hand in the browser pane against the running API.

## Needs your decision

1. **Inbox actions (#29, `contract`).** `InboxItemDto` doesn't say which actions a task allows, but UI_SPEC §4.6 wants inline buttons. FE-10 derives them from `kind` in one helper (`actionsForTaskKind`, Approval → approve/reject, Fulfillment → fulfill). That copies a domain rule into the frontend. Proposal: BE-12 adds `actions` to `InboxItemDto`; the helper is then deleted. Please say yes or no before BE-12 is built.
2. **Error-code name.** Your FE-13 brief said `user.self_change_forbidden`. The backend and contract use `user.self_administration` (already mapped in `problem.ts`), and `problem.test.ts` rejects a message for a code the backend doesn't define. I used the existing code and opened #27 (now closed: BE-08b added self-deactivation under the same code, so the UI lock on "Active" matches). If you want the code renamed, that's a backend + contract + openapi + `problem.ts` change in one PR.
3. **`main` checkout has an uncommitted edit** to `src/backend/FlowOps.Api/Controllers/NotificationsController.cs`. It isn't mine; I didn't touch it.

## FE-13 — Administration screens

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/28 (merged)
- **Checks (src/frontend):** `npm run typecheck` ok · `npm run lint` ok ("Design-token check passed.") · `npm test` 436 passed / 0 failed · `npm run build` ok.
- **What:** users list (search, department, role, include-inactive, paging, all in the URL), create/edit form (department select, searchable manager picker, role checkboxes, active), reset-password dialog, departments table + dialog, stalled-tasks table with guidance per reason. New shared `Checkbox`; `useDebouncedUrlSearch` extracted from `RequestFilters` (two callers).
- **Self-change rule:** on the admin's own record roles, manager and Active are disabled with an explanation, the form sends the stored values for those fields, and the admin is excluded from the manager picker. The server's 422 is still mapped and shown in the banner (tested).
- **`/review-ui`:** run by me against the checklist (not a separate reviewer agent). Two Minors fixed (a Retry button on a 404; no heading/announcement while loading). Later, in the browser, one **Major** appeared with real data: the Users table clipped its last column at about 800 px — fixed in the third PR.
- Stalled tasks stay on MSW until BE-15.

## FE-10 — Approval inbox

- **Status:** Done (mocks until BE-12) · **PR:** https://github.com/osamashtayat/flowops/pull/30 (merged)
- **Checks:** typecheck ok · lint ok · `npm test` 447 passed / 0 failed (three consecutive runs) · build ok.
- **What:** `/inbox` with Open / Completed tabs (`?status=`), inline decisions through the existing `DecisionActions` → `DecisionDialog` → `useDecideTask` (reject needs a reason; 409 → mapped toast and refresh), outcome and decided date on Completed, empty/error/loading states. The mock `/api/tasks` now derives from the mock request store, so a decision moves the item from Open to Completed; the sidebar badge counts changed from 5 to 3 for Finance accordingly.
- **`/review-ui`:** no Blockers/Majors in my read. Merged requester and department into one column so the action buttons fit.
- In the dev browser the inbox is mocked while the request pages are real, so an inbox "View" link points at a mock request id the real API doesn't know. This ends when BE-12 merges.

## Real-API switch (new request after BE-10; lists, details, activity, comments after BE-11)

- **Status:** Done · **PR:** https://github.com/osamashtayat/flowops/pull/34 (merged)
- **Checks:** typecheck ok · lint ok · `npm test` 28 files / 448 passed / 0 failed · build ok (also re-run after merging `main`).
- **Code:** the dev browser already mocked only endpoints without a merged backend (dashboard, tasks, unread count, stalled tasks), so no routing change was needed; admin users/departments also use the real API since BE-08. Tests keep MSW for everything.
- **Browser test** (Vite on :5173 → API on :7181 with a throwaway Postgres on :5433; your dev database was not touched): sign-in with must-change password and `returnTo` · My requests empty and filled · request details as requester and as manager · comment · leave form blocked without a manager · Approve dialog, whose call answers 501 and shows "This feature isn't available yet." · admin users list · own record locked · manager picker (no admin, no self) · create/edit/roles save · departments.
- **Leave days:** checked against the API, matches the client: inclusive, max 90. 1 Jan–31 Mar 2027 → 201; 1 Jan–1 Apr → 400 `errors.endDate`; past start and end-before-start → 400. No `contract` issue needed.
- **Real-data fixes:** (1) **bug:** after saving a user the page stayed on the form, no toast: the refetch changed the `key` on `UserForm`, remounting it and dropping the success handler. Removed the key; verified in the browser. No unit test: the old code also passes in jsdom. (2) Users table clipped at ~800 px (name and email now share a cell; `DataTable` scrolls inside its card). (3) Request number wrapped; one-day leave showed "3 May – 3 May".
- Nulls, empty states and error states from real responses rendered correctly everywhere I looked.

## Issues

- Opened: #27 `[FE-13]` self-deactivation / error code name (closed by BE-08b), #29 `[FE-10]` `InboxItemDto` actions (open).
- I created the missing labels (`backend`, `frontend`, `contract`, `docs`, `infra`, `blocker`, `decision-needed`, `security`, `tech-debt`).
- Seen, not mine: #31, #32 (backend, BE-08b).

## Not done / not verified

- Dashboard, inbox, notifications and stalled tasks were not looked at in a browser: the pane blocked MSW's service worker ("mock API could not start"), so they were checked only in unit tests.
- No visual check at 360 and 1280 px; I saw about 800 and 1024 px. Mobile layout of the new screens is reasoned from the code.
- Decisions (approve / reject / fulfil) against a real backend: untested until BE-12.
- `/review-ui` passes were my own read, not `ui-ux-reviewer`; no `verify-feature` pass.
- Follow-up, not done: `LoginPage` / `ChangePasswordPage` still use their own banner markup instead of `Alert`.
- Test data: a throwaway container and its accounts were removed at the end; the API and Vite processes I started were stopped by port.

## Addendum — 2026-11-07

- **#29 (inbox `actions`) — Done:** https://github.com/osamashtayat/flowops/pull/42 (merged). `InboxItemDto.actions` added to the contract; the inbox renders `item.actions` and the kind-based helper is gone. BE-12 must fill it (written into the BE-12 entry). Checks: typecheck ok, lint ok, 449 frontend tests passed, `dotnet build` 0 errors, `FlowOps.Domain.Tests` 103 passed, contract-drift integration filter 130 passed.
- **#43 opened (backend, decision-needed):** whether comments on leave requests should be masked like the leave reason. Today they are not, by design (AUTHORIZATION §5).
- **#38 (leave visibility warning) — Done:** the leave comment box and the decision dialog's comment / reason field (approve, reject, fulfil) now say "Visible to everyone who can see this request. Don't repeat the leave reason." Shown for leave requests only, from the request page and from the inbox (the dialog gets `requestType` through `DecisionActions`). One wording home: `lib/format/visibility.ts`; no backend change. Checks (src/frontend): `npm run typecheck` ok · `npm run lint` ok ("Design-token check passed.") · `npm test` 29 files / 460 passed / 0 failed · `npm run build` ok. New tests: `features/requests/leaveVisibility.test.tsx` (comment box, dialog from request page, dialog from inbox, purchase has no warning). `/review-ui`: text-only change through the existing `FormField` helper; I read it against the checklist, no browser check this time (no API running). Minor, not fixed: on leave the approve/fulfil helper reads "Optional. Visible in the request history. Visible to everyone…", which says "visible" twice.
