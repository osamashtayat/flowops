---
paths:
  - "tests/**"
  - "src/frontend/**/*.test.ts"
  - "src/frontend/**/*.test.tsx"
  - "src/frontend/src/test/**"
---

# Testing rules

Full strategy and required scenarios: `docs/TESTING.md`.

## What to test
- Domain: every invariant and transition, success and failure (`TESTING.md` §3.1).
- API integration (real PostgreSQL via Testcontainers): workflows, authorization (404 vs 403 vs 401), validation, concurrency, notifications (§3.2–3.5).
- Frontend: behaviour through the UI with Testing Library + MSW; forms, guards, error/empty/loading states (§3.6).
- Every bug fix starts with a failing test.

## How
- Arrange through the API and builders (`Given.User(...)`, `ClientFor(user)`), not by poking the DbContext, except bulk org setup.
- No mocked DbContext. Fakes allowed for `TimeProvider` (`FakeTimeProvider`) and `IRealtimePublisher` (recording fake).
- Concurrency tests run the race many times (≥ 20) and assert exactly one winner plus consistent state.
- Assert error responses by status **and** `code` (`ProblemAssert.HasCode`).
- Names describe behaviour: `Reject_WithoutReason_Returns400`. One behaviour per test.
- Frontend: query by role/label/text, not test ids or classes; `userEvent` not `fireEvent`; MSW handlers typed from `schema.d.ts`.
- No sleeps or real timers for waiting; no order-dependent tests; no shared mutable state across tests.

## Honest reporting
- Run the tests you wrote. Report the exact command and the pass/fail/skip counts.
- If you couldn't run something (e.g. Docker unavailable), say so explicitly. Never claim "tests pass" without running them.
- Report coverage gaps you noticed but didn't fill.
