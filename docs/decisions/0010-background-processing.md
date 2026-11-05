# ADR-0010: No background job framework in Phase 1

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
The prompt asked to evaluate Hangfire vs .NET BackgroundService. Phase 1 has no scheduled or deferred work: all processing happens synchronously within requests, and process state is fully persisted.

## Decision
Use neither in Phase 1. If a small periodic task is needed before Phase 3 (e.g. cleanup), use a `BackgroundService` with `PeriodicTimer`, idempotent and safe to run on multiple instances. Adopt Hangfire (PostgreSQL storage) when Phase 3 introduces reminders/escalations requiring persistence, retries, scheduling and a dashboard.

## Consequences
+ No extra schema, dashboard, or failure modes now.
- Phase 3 adds a dependency and migration (planned).
Comparison: BackgroundService — built in, no persistence/retries/scheduling, good for simple loops. Hangfire — persistent jobs, retries, delayed/recurring jobs, dashboard, PostgreSQL storage available; heavier, licence check needed for Pro features (not needed).

## Alternatives rejected
Introducing Hangfire for notifications delivery (synchronous is sufficient); Quartz.NET (comparable; reconsider in Phase 3).
