# Agent Handoff — FlowOps

**Purpose:** how separate coding sessions (different models, different days) collaborate without drifting apart. Assume every session starts with zero memory of previous ones; everything it needs is in the repository.

---

## 1. Reading order per session type

| Session | Always read | Then read |
|---|---|---|
| **Any** | `AGENTS.md`, the task entry in `PHASE1_TASKS.md` | `.claude/rules/*` load automatically in Claude Code; other tools: read the matching rule files |
| Backend feature | `ARCHITECTURE.md`, `DOMAIN_MODEL.md`, `CODE_STANDARDS.md` | `WORKFLOWS.md` and `AUTHORIZATION.md` for anything touching requests/tasks/access; `API_CONTRACT.md` for endpoints; `TESTING.md` |
| Frontend feature | `UI_SPEC.md`, `DESIGN_SYSTEM.md`, `CODE_STANDARDS.md` | `API_CONTRACT.md`, generated `src/frontend/src/lib/api/schema.d.ts`; `TESTING.md` §3.6 |
| Test work | `TESTING.md`, the feature's docs | |
| Review | the diff, `CODE_STANDARDS.md` §12 checklist, relevant ADRs | |

Before writing new code, **search for existing implementations** of the shared knowledge listed in `CODE_STANDARDS.md` §2.3.

## 2. Ownership boundaries

| Area | Owner lane | Others may |
|---|---|---|
| `src/backend/**`, `tests/backend/**` | Backend | read |
| `src/frontend/**` | Frontend | read |
| `contracts/openapi.json` | Backend (generated) | read; frontend regenerates types from it |
| `docs/API_CONTRACT.md` | Shared — changes per §3 | |
| `docs/DOMAIN_MODEL.md`, `WORKFLOWS.md`, `AUTHORIZATION.md` | Backend (with review) | propose changes via a note in the PR |
| `docs/UI_SPEC.md`, `DESIGN_SYSTEM.md`, `theme.css` | Frontend | |
| `docs/decisions/*` | Anyone, by new ADR only | |
| `AGENTS.md`, `CLAUDE.md`, `.claude/**` | Architecture changes only (explicit task) | |

Parallel sessions work on separate branches named `task/<ID>-short-name` (e.g. `task/BE-10-submit-requests`). Never two sessions on the same task ID.

## 3. API contract changes

1. A frontend session that needs a field or endpoint that doesn't exist **stops and reports it**; it does not invent it or hand-edit generated types. It may add a `Contract request` entry to §6 below.
2. A backend session implements the change: updates `API_CONTRACT.md`, the DTO/controller, regenerates `contracts/openapi.json`, and lists the change under **Contract change** in its summary.
3. The frontend regenerates `schema.d.ts` (`npm run api:types`); TypeScript errors show every place that must adapt.
4. Breaking changes are made additive first (new field), consumers migrate, then the old field is removed in a later task.

## 4. Architecture decisions and conflicts

- Existing decisions are in `docs/decisions/`. Code must follow them.
- If a task can't be done well within an ADR or the contract, the agent **flags the conflict** in its final report (and in §6) with: the ADR/section, why it blocks, the proposed alternative. It does not silently diverge.
- A new decision = a new ADR file (`NNNN-title.md`, status `Proposed`) in the same PR, accepted by the human owner.

## 5. End-of-session report (required format)

```
## Task: BE-10 — Submit purchase & leave
Status: Done | Partially done | Blocked
### Changes
- files/areas touched (grouped by layer)
### Contract change
- none | description + regenerated openapi.json
### Decisions taken
- … (link ADR if any)
### Tests
- Command(s) run and exact results (e.g. "dotnet test …: 41 passed, 0 failed")
- Not run: … and why
### Docs updated
- …
### Follow-ups / risks
- … (duplication spotted, tech debt, questions)
```
Update the task's status line in `PHASE1_TASKS.md` in the same change.

## 6. Open coordination items
Use this list for cross-lane requests that can't be resolved inside one task. Remove entries when resolved.

| Date | From | Item | Status |
|---|---|---|---|

## 7. Ready-to-use prompts for the first sessions

### First backend session — BE-01 (then BE-06)
```
You are working in the FlowOps repository. Read AGENTS.md, then docs/ARCHITECTURE.md §2–4,
docs/CODE_STANDARDS.md and the BE-01 entry in docs/PHASE1_TASKS.md.

Task: /implement-feature BE-01 — create the backend solution exactly as specified:
FlowOps.slnx, src/backend/FlowOps.{Domain,Application,Infrastructure,Api},
tests/backend/FlowOps.{Domain.Tests,Api.IntegrationTests}; project references exactly per
ARCHITECTURE.md §3; Directory.Build.props (net10.0, Nullable, ImplicitUsings,
TreatWarningsAsErrors, AnalysisLevel latest-recommended); Directory.Packages.props (central
package management); Api with controllers, GET /api/health (anonymous), JSON console logging,
AddProblemDetails, launchSettings HTTPS on https://localhost:7180; no WeatherForecast.
Add an architecture test that fails if any project reference violates ARCHITECTURE.md §3.

Constraints: no business code yet; no MediatR/AutoMapper/repositories; keep global.json as is
unless the installed SDK requires a change (then explain).
Verify: `dotnet build` (0 warnings) and `dotnet test`; report exact output.
Then, if time remains, start BE-06 (pure domain) with /implement-feature BE-06 in a separate
commit. Finish with the report format in docs/AGENT_HANDOFF.md §5 and update task statuses.
```

### First frontend session — FE-02
```
You are working in the FlowOps repository. Read AGENTS.md, docs/DESIGN_SYSTEM.md (all),
docs/UI_SPEC.md §4.4 and §5, docs/CODE_STANDARDS.md §1–2, and the FE-02 entry in
docs/PHASE1_TASKS.md. Inspect src/frontend/src (theme.css, components/ui/Button.tsx,
StatusBadge.tsx, lib/format/status.ts) — follow their patterns.

Task: /implement-feature FE-02 — build the form primitives in src/frontend/src/components/ui:
Input, Textarea, Select, DateInput, MoneyInput (JOD, up to 3 decimals, value kept as a string),
FormField (label, required marker, helper, error with aria-describedby/aria-invalid), Card,
and components/layout/PageHeader. Add react-hook-form + zod and a small helper that wires a
zod schema to FormField errors. Show all primitives on the FoundationPage (since removed by FE-12).

Constraints: theme tokens only (no hex/rgb, no arbitrary or default-palette colour classes);
control outlines use border-border-strong; visible focus; no API calls (that is FE-01, blocked
on BE-03); don't invent API fields — MoneyInput's value shape follows docs/API_CONTRACT.md §1.
Tests (Vitest + Testing Library + user-event): label/error association, required marker,
MoneyInput decimal handling and paste, keyboard operation.
Verify: npm run typecheck && npm run lint && npm test && npm run build — report exact output.
Then run /review-ui on your changes, fix Blockers/Majors, and finish with the
docs/AGENT_HANDOFF.md §5 report.
```
