---
name: implement-feature
description: Implement one FlowOps backlog task (e.g. BE-10, FE-07) end to end following the project's architecture, DRY and testing rules. Use when starting work on a task ID from docs/PHASE1_TASKS.md.
argument-hint: "[TASK-ID]"
arguments: [task]
---

Implement backlog task **$task** for FlowOps.

## 1. Understand the task
- Find `$task` in `docs/PHASE1_TASKS.md`. If its dependencies aren't `Done`, stop and report which are missing.
- Read every doc the task references, plus `docs/AGENT_HANDOFF.md` §1 for your session type.
- Restate the objective and acceptance criteria in 3–6 bullets. If anything is ambiguous or conflicts with a doc or ADR, list it now; resolve from the docs, or flag it — do not guess silently.

## 2. Survey before you build
- Search for existing code that already owns the knowledge you need (`docs/CODE_STANDARDS.md` §2.3). Example searches: `AccessRules`, `TaskEligibility`, `FixedProcesses`, `NotificationFactory`, `ErrorCodes`, `queryKeys`, `components/ui/`.
- Note what you'll **reuse**, what you'll **extend**, and what is genuinely **new**. A new shared abstraction needs ≥ 2 real callers.
- Check `docs/API_CONTRACT.md` and `contracts/openapi.json` for every endpoint/field you'll touch. Missing contract → follow `docs/AGENT_HANDOFF.md` §3; frontend sessions stop and report.

## 3. Plan a small change
Write a short plan: files to add/change per layer, tests to add, docs to update. Keep scope to the task; note unrelated issues as follow-ups.

## 4. Implement in the right layer
- Backend: domain rules in the aggregate → application service orchestration (one `SaveChangesAsync`, publish after commit) → thin controller. DTOs at the boundary.
- Frontend: API hook in `features/*/api.ts` → shared components → page composition. Tokens only.
- Re-check as you go: is any rule being written a second time?

## 5. Test
- Add the tests the task lists and the relevant scenarios from `docs/TESTING.md` §3, including failure and authorization paths.
- Run them:
  - backend: `dotnet build` then `dotnet test tests/backend/FlowOps.Domain.Tests` and, with Docker, `dotnet test tests/backend/FlowOps.Api.IntegrationTests`
  - frontend (in `src/frontend`): `npm run typecheck && npm run lint && npm test && npm run build`
- If the contract changed: regenerate `contracts/openapi.json` and frontend types.

## 6. Self-review
Walk the checklist in `docs/CODE_STANDARDS.md` §12. Fix what you find.

## 7. Report
- Update the task's status line in `docs/PHASE1_TASKS.md` and any affected docs.
- Finish with the report format in `docs/AGENT_HANDOFF.md` §5, with **exact** test commands and results. State plainly anything not run.
