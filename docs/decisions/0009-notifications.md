# ADR-0009: Notifications persisted in the business transaction; SignalR as post-commit hint

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
Users need durable notifications and live updates. Real-time messages must never describe data that was rolled back.

## Decision
Notification rows are created by `NotificationFactory` and saved in the same `SaveChangesAsync` as the state change. After commit, `IRealtimePublisher` sends id-only SignalR events; clients invalidate queries and refetch via REST. Publish failures are logged, not propagated. No outbox in Phase 1.

## Consequences
+ Durable notifications are exactly consistent with state.
+ Lost real-time messages only delay freshness; clients refetch on reconnect/focus.
- A crash between commit and publish drops that live push (acceptable; data is durable).
- When webhooks/external integrations arrive (Phase 3), an outbox becomes necessary.

## Alternatives rejected
Outbox + background dispatcher now (infrastructure without a delivery-guarantee requirement); sending full payloads over SignalR (two sources of truth).
