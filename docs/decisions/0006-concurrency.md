# ADR-0006: Optimistic concurrency with PostgreSQL xmin and a partial unique index

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
Two officers in a role queue can decide the same task simultaneously; stale browser tabs can submit decisions for already-advanced requests. Exactly one decision must win and the request must never advance twice.

## Decision
`xmin` mapped as concurrency token on Request and WorkTask. Decision endpoints require the client's last-seen `version`; mismatch → 409 before work. Save conflicts (`DbUpdateConcurrencyException`) → 409 `concurrency.conflict`. Defence in depth: partial unique index allowing one Open task per request. One `SaveChangesAsync` per use case gives atomicity without explicit transactions. No row locks, no retries.

## Consequences
+ No extra columns; no lock contention; simple to test deterministically.
+ Conflicts surface as honest user messages.
- Users occasionally see "already handled" (correct behaviour).
- `xmin` is PostgreSQL-specific (we're PostgreSQL-only).

## Alternatives rejected
`SELECT … FOR UPDATE` (pessimistic; holds locks during user-irrelevant work, more complex); application-level version columns (duplicates xmin); distributed locks (unnecessary in a monolith).
