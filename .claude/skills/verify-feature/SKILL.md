---
name: verify-feature
description: Verify that a completed FlowOps backlog task actually meets its acceptance criteria, business rules, authorization, API contract, persistence, error handling, tests and code-quality standards. Use after implementation and before marking a task Done.
argument-hint: "[TASK-ID]"
arguments: [task]
---

Verify task **$task** against the specification. Report evidence, not impressions.

## 1. Collect the spec
From `docs/PHASE1_TASKS.md` ($task), plus the referenced sections of `docs/WORKFLOWS.md`, `docs/AUTHORIZATION.md`, `docs/DOMAIN_MODEL.md`, `docs/API_CONTRACT.md`, `docs/UI_SPEC.md`, `docs/TESTING.md` §3. Turn them into a checklist.

## 2. Check each item and record evidence

| Area | How to verify |
|---|---|
| Acceptance criteria | Find the code path and the test that proves each one |
| Business rules | Invariants I1–I15 relevant to the task are enforced in the domain (not only in the service/UI) and tested |
| Authorization | Each permission-matrix cell touched has a positive and a negative test; 401/403/404 semantics correct; actor from `ICurrentUser` |
| API contract | Routes, DTO fields, types, status codes and error `code`s match `API_CONTRACT.md` and `contracts/openapi.json`; frontend uses generated types |
| Persistence | One `SaveChangesAsync` per use case; constraints/indexes per DOMAIN_MODEL §6; migration present and applies; projections for reads |
| Error handling | Typed exceptions → ProblemDetails via global handler; no swallowed errors; frontend shows mapped messages |
| Tests | Run them now (commands in `docs/TESTING.md` §4); record exact pass/fail/skip counts |
| Code quality | `docs/CODE_STANDARDS.md` §12 checklist; no duplicated knowledge (§2.3) |
| UI (FE tasks) | Loading/empty/error states, tokens only (`npm run lint:tokens`), accessibility basics |

## 3. Report
```
## Verification: $task
Verdict: Meets spec | Meets spec with minor issues | Does not meet spec
### Evidence
- [✔/✘] criterion — file:line / test name / command output
### Problems
- severity · location · what fails · suggested fix
### Not verified
- item and why (e.g. Docker unavailable)
```
Never mark an item ✔ without evidence. Don't fix problems in this skill unless asked; report them.
