---
name: review-change
description: Review a FlowOps code change (current diff, branch, or given paths) for correctness, security, regressions, separation of concerns, DRY violations, unnecessary abstractions, missing tests and documentation drift, with severity-rated, file-referenced recommendations.
argument-hint: "[base-ref or paths, default: main]"
---

Review the change set: $ARGUMENTS (if empty, review `git diff main...HEAD` plus uncommitted changes).

## 1. Scope
Run `git diff --stat` and read every changed file fully (not only hunks) plus the callers/callees needed to judge it. Identify the task ID if any and read its entry in `docs/PHASE1_TASKS.md`.

## 2. Review dimensions
1. **Correctness** — does it do what the task/docs specify? Edge cases from `docs/WORKFLOWS.md` §4?
2. **Security** — `.claude/rules/security.md`; actor identity, access helpers, 404/403, sensitive data, CSRF.
3. **Regressions** — behaviour or contract changes affecting other features; removed tests; changed error codes.
4. **Separation of concerns** — right layer per `docs/CODE_STANDARDS.md` §1.
5. **DRY** — any knowledge from §2.3 re-implemented? Purchase vs leave duplication? Frontend repeating a component, API call or query key?
6. **Abstractions** — new interfaces/classes justified by ≥ 2 real uses? Phase 2/3 machinery?
7. **Tests** — success, failure and authorization paths covered? Concurrency for decision paths?
8. **Docs & contract drift** — `API_CONTRACT.md`, `contracts/openapi.json`, generated frontend types, domain/workflow docs, task status.

For large or risky changes (auth, access rules, decisions, migrations), also delegate to the `security-reviewer` and/or `architecture-reviewer` agents and merge their findings.

## 3. Output
```
## Review: <scope>
Summary: 1–3 sentences; merge recommendation (Approve / Approve with changes / Request changes)

### Findings
| Sev | Location | Issue | Recommendation |
|---|---|---|---|
| Critical/High/Medium/Low | path:line | … | concrete change |

### Missing tests
- …
### Docs/contract to update
- …
```
Severity: **Critical** security or data-integrity flaw · **High** spec violation, duplicated business logic, wrong layer for a rule · **Medium** maintainability/test gap · **Low** minor clarity. Skip pure formatting. Don't edit files unless asked.
