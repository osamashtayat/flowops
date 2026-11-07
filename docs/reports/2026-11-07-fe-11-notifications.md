## Task: FE-11 — Notifications & realtime
Status: Done for notifications (real API, BE-13). Realtime client built and unit-tested, **not verifiable against a real hub**: BE-14 is not on `main` (issue #53).

### Changes
- `features/notifications`: `api.ts` (list, unread count, mark read with the only optimistic update + rollback + toast, mark all read), `NotificationBell` (unread badge, popover with the latest 8, "View all"), `NotificationList` / `NotificationFeed` (loading, error with retry, empty), `NotificationsPage` (unread-only filter and paging in the URL, "Mark all as read"), `targets.ts` (click routing: role-change types → `/admin/role-changes`, otherwise the request, a bare task → `/inbox`).
- `lib/realtime`: `invalidation.ts` (event → query keys per API_CONTRACT §9; reconnect → everything), `connection.ts` (SignalR client, quiet capped retry when the hub is missing), `useRealtime.ts` (mounted in `AppShell`; off in tests and while the dev API is mocked).
- TopBar uses the bell; `/notifications` route replaced the placeholder; MSW notification handlers + store (tests only; dev now uses the real endpoints); dependency `@microsoft/signalr`.

### Contract change
- none (generated types only gained aliases in `lib/api/types.ts`).

### Decisions taken
- The popover is a small local component (single use) rather than a new `components/ui` primitive.
- Mark-all-read is not optimistic (only marking read is allowed to be).
- The rejection notification body repeats the decider's reason (API_CONTRACT §5/§6); it is rendered as plain text for the requester, as the contract defines. No leave reason is ever shown, and nothing is logged (SignalR logging is off).

### Issues opened or updated
- #53 [BE-14] Hub details the FE-11 SignalR client relies on (backend, contract)

### Tests
- `cd src/frontend && npm run typecheck`: ok · `npm run lint` (oxlint + design tokens): ok · `npm test`: 494 passed, 0 failed (31 files; 20 of them new, in `notifications.test.tsx` and `realtime.test.ts`) · `npm run build`: ok.
- Real API (`VITE_API_MOCKS=off`, throwaway Postgres on :5433, Playwright headless): bell count and popover; View all; click → request route; optimistic badge drop persisted on the server; unread-only filter; mark all read → empty state and count 0; employee gets StepApproved and RequestRejected after the manager decides; `RoleChangePending` (admin B) and `RoleChangeApproved` (admin A) open `/admin/role-changes`. 
- Not run: live realtime (no hub: negotiate → 404); `/admin/role-changes` renders the not-found page until FE-13b merges.

### Docs updated
- `docs/PHASE1_TASKS.md` (FE-11 status and notes), this report.

### Follow-ups / risks
- Verify realtime end to end once BE-14 is merged (#53). Nothing else needed in the client if the hub matches §9.
- After FE-13b merges, check the role-changes list also refetches on `notificationCreated` (the contract lists no event for it).
- `PlaceholderPage` is now unused; delete it in a cleanup.
