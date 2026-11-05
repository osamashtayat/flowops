# ADR-0005: Request/Stage/WorkTask state model with a single WorkTask entity

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
We must distinguish the overall request state, the current process stage and individual task state, and keep approval and fulfillment mechanics consistent.

## Decision
`Request.Status` (InReview, Approved, Completed, Rejected), `Request.Stage` (current step or Closed), and `WorkTask` (Open/Done + Outcome) as defined in DOMAIN_MODEL.md. Approval and fulfillment are one `WorkTask` entity with `Kind`; the decision (outcome, actor, time, comment) is stored on the task — no separate `ApprovalTask`, `FulfillmentTask` or `ApprovalDecision` entities. Status and Stage change only inside aggregate methods. Activities are an append-only log on the aggregate.

## Consequences
+ One inbox query, one eligibility rule, one decision pipeline, one concurrency token.
+ Fewer tables and joins.
- Task rows carry nullable decision columns (guarded by check constraints).
- If Phase 3 needs multiple decisions per task (e.g. multi-signer), a decision table returns.

## Alternatives rejected
Separate task tables per kind (duplicated logic); a generic state-machine library (opaque for a 4-state model).
