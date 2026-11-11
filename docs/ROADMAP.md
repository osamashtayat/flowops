# Roadmap — FlowOps

**Purpose:** how the architecture evolves across phases, and which Phase 1 seams make that possible. Nothing here is built in Phase 1 unless listed under Phase 1.

---

## Phase 1 — Functional business application (current)
Two fixed processes, approvals inbox, notifications, admin, tests. See `PRD.md` §4 and `PHASE1_TASKS.md`.

**Seams deliberately left for later phases** (cheap now, expensive to retrofit):

| Seam | Phase 1 form | Enables |
|---|---|---|
| `FixedProcesses` → `ProcessStep(Stage, Kind, AssigneeRule)` | C# data | Phase 2 replaces the source with persisted definitions; `Request.DecideTask` keeps working |
| Single `WorkTask` with `Kind` and user-or-role assignee | One table | Phase 2 dynamic steps, Phase 3 parallel tasks (drop the one-open-task index, add join semantics) |
| `AssigneeRule` (`RequesterManager`, `Role`) | 2 cases | Phase 2 adds `Department(x).Role`, `SpecificUser`, `ManagerOfManager` |
| Activity log as append-only events | Audit timeline | Phase 3 analytics, SLA metrics, webhooks (events already exist) |
| `IRealtimePublisher` post-commit | Direct SignalR | Phase 3 outbox + background dispatcher + webhooks |
| Request details as typed 1:1 tables | Two detail types | Phase 2 dynamic forms: add `FormSubmission` (jsonb) beside typed details; existing types stay typed |
| Capabilities from `/auth/me` | 3 capabilities | Permission growth without frontend role logic |

## Phase 2 — Configurable workflow engine

> **Design in progress:** ADR-0014 (Proposed) refines this section, covering engine evaluation, skip semantics, open questions Q1–Q7. The backlog is in `PHASE2_TASKS.md`. Where they differ, ADR-0014 wins once Accepted.

1. **Workflow definitions** — `WorkflowDefinition` (name, request type, version, status Draft/Published), `WorkflowStepDefinition` (order, kind, assignee rule, optional condition). Published definitions are immutable; requests pin the version they started on.
2. **Form definitions** — field schema (text, number, money, date, select) stored as jsonb; server-side validation from the schema; frontend renders forms from the same schema.
3. **Execution** — `Request` stores `WorkflowDefinitionId` + `Version`; `FixedProcesses` becomes `IProcessDefinitionSource` with two implementations (fixed, persisted) during migration, then fixed is removed. The aggregate's `DecideTask` contract is unchanged.
4. **Conditional branching** — simple, typed predicates on form fields (`amount > 500`), not a scripting language.
5. **Visual editor** — linear step list with conditions first; a graph canvas only if parallel branches arrive.
6. **Reassignment & delegation** — admin reassign, out-of-office delegate.

Migration: existing purchase/leave become two seeded published definitions; their typed detail tables remain.

## Phase 3 — Advanced enterprise features

| Feature | Architectural change |
|---|---|
| Escalations, reminders, SLAs | Background processing (Hangfire or Quartz with PostgreSQL storage — revisit ADR-0010) |
| Parallel approvals (all/any) | Multiple open tasks per step; join rule on step; remove I4 index |
| Workflow versioning & migration tools | Definition versions already pinned in Phase 2 |
| Webhooks / ERP integrations | Transactional **outbox** table + dispatcher; signed webhook deliveries; retries |
| Analytics | Read models built from activities; possibly a reporting schema |
| Multi-company isolation | `TenantId` on every aggregate + EF global query filters + per-tenant roles; membership table for users in several companies |
| External API clients / mobile | Token auth (OAuth2/OIDC via an identity provider) alongside cookie SPA auth |
| SSO | OIDC with Entra ID / Google Workspace |
| Monitoring | OpenTelemetry traces/metrics export |

## Explicitly rejected (all phases unless new requirements appear)
Microservices, event sourcing as the system of record, BPMN engine embedding, generic repositories, a plugin marketplace.
