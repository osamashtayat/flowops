# FlowOps — Instructions for Coding Agents

FlowOps is a business-process platform: employees submit **equipment purchase** and **leave** requests; managers and finance/HR/procurement officers approve or fulfil them through an inbox; admins manage the organisation. Stack: ASP.NET Core (.NET 10) + EF Core + PostgreSQL + Identity + SignalR; React + TypeScript (strict) + Vite + Tailwind v4 + TanStack Query.

**Current phase: Phase 1** (fixed processes). Do not build Phase 2/3 features (configurable workflows, form builder, escalations, webhooks, multi-tenancy). See `docs/ROADMAP.md`.

This file is model-agnostic and is the shared source of truth for every agent. Detail lives in `docs/`; do not duplicate it here.

---

## 1. Before you write code

1. Read your task in `docs/PHASE1_TASKS.md` and the docs it references.
2. Read `docs/AGENT_HANDOFF.md` §1 for what to read per session type.
3. **Search for existing implementations first** — shared knowledge has one home (table in `docs/CODE_STANDARDS.md` §2.3). Reuse it; don't re-implement it.
4. Plan the smallest change that satisfies the acceptance criteria.

## 2. Map of the repository

| Path | What |
|---|---|
| `docs/` | PRD, ARCHITECTURE, DOMAIN_MODEL, WORKFLOWS, AUTHORIZATION, API_CONTRACT, UI_SPEC, DESIGN_SYSTEM, CODE_STANDARDS, TESTING, PHASE1_TASKS, PHASE2_TASKS, ROADMAP, AGENT_HANDOFF, `decisions/` (ADRs) |
| `src/backend/FlowOps.{Domain,Application,Infrastructure,Api}` | Backend layers (created by BE-01) |
| `tests/backend/` | `FlowOps.Domain.Tests` (fast), `FlowOps.Api.IntegrationTests` (Docker) |
| `src/frontend/` | React app; tokens in `src/styles/theme.css` |
| `contracts/openapi.json` | Generated executable API contract (from BE-03) |
| `.claude/` | Claude Code rules, specialist agents, skills (other tools: read `.claude/rules/*.md` as plain guidance) |

## 3. Mandatory rules

**Architecture**
- Controllers are thin: bind → authorize (attribute) → call **one** application service → map to HTTP. No business rules, no `DbContext`, no `try/catch`.
- Business invariants and state transitions live in the **Domain** (`Request` aggregate, `WorkflowVersion`). Use-case orchestration lives in **Application** services. Technical adapters live in **Infrastructure**.
- Dependency direction: Domain ← Application ← Infrastructure; Api composes. Never reference Api/Infrastructure from Application/Domain.
- Database access only through `IAppDbContext` inside Application services; one `SaveChangesAsync` per use case; real-time publishing only after commit.
- No generic repositories, no MediatR, no AutoMapper, no new abstraction without ≥ 2 real callers.

**DRY (knowledge, not syntax)**
- Visibility → `AccessRules`; decide-eligibility → `TaskEligibility`; step order → the pinned `WorkflowVersion` via `IProcessDefinitionSource`; assignee → `TaskAssigneeResolver`; notifications → `NotificationFactory`; errors → `ErrorCodes` + global handler. Never copy these rules elsewhere — including the frontend.
- Don't merge things that only *look* alike (CODE_STANDARDS §2.2).

**Security**
- Authorization is enforced server-side. Never trust user ids, roles, departments or ownership sent by the client; the actor comes from `ICurrentUser`.
- Invisible resources return 404; visible-but-forbidden return 403. Administrators have no process authority.
- Never log or expose leave reasons, comments, passwords or tokens.

**Contracts**
- `docs/API_CONTRACT.md` + `contracts/openapi.json` are authoritative. **Do not invent endpoints or fields.** Contract changes follow `docs/AGENT_HANDOFF.md` §3.
- Frontend calls the API only via `src/frontend/src/lib/api/` with generated types; never hand-edit `schema.d.ts`.

**Frontend & UI**
- Reuse components from `components/ui` and `components/layout`; build a new shared component instead of a page-local copy when a pattern repeats.
- Server state only in TanStack Query via feature hooks and central query keys.
- Colours, type and radii only from theme tokens (`docs/DESIGN_SYSTEM.md`). No hex/rgb literals, no arbitrary colour classes, no orange-led or black-led styling. WCAG AA.
- Permissions in the UI come from the server's `capabilities` / `availableActions`, never from role-name logic.

**Process**
- Tests are required for important behaviour, including failure and authorization paths (`docs/TESTING.md`).
- Do not silently change an architectural decision; flag conflicts (`docs/AGENT_HANDOFF.md` §4).
- No unrelated refactoring; report it as a follow-up instead.
- Never fabricate test results. Report exactly what you ran and what happened.
- Follow the branch workflow (§4) and raise problems as GitHub issues (§5). Both are mandatory.

## 4. Branch workflow (mandatory, every task)

Every backlog task, however large, follows exactly this flow:

1. **One task = one branch.** Create a single branch from the latest `origin/main`, named `task/<ID>-short-name` (e.g. `task/BE-10-submit-requests`). Do all of the task's work on it. Don't split a task across several branches, don't stack branches on other unmerged branches, and don't put two tasks on one branch.
2. **Use your own git worktree** for that branch (`git worktree add`). Never switch branches or commit in the main checkout; several agents share it.
3. **Keep it current.** Before merging, update the branch with the latest `main` (merge or rebase) and fix any conflicts on the branch.
4. **Finish = merged into `main`.** When the build and all required tests are green and the Definition of Done (§8) is met: push, open one PR into `main`, merge it (squash), and delete the branch. A task is not done while its work sits only on a branch.
5. **If it can't be finished green:** leave the PR open and unmerged, open a GitHub issue explaining why (§5), and say so in your report. Never merge red, and never merge with tests you didn't run.

Small fixes found during a task belong on that task's branch. Unrelated fixes get their own issue (§5) and, later, their own task branch.

## 5. Raising problems: open a GitHub issue, never stay silent

If you hit a bug, a blocker, a contract gap, a conflict with an ADR or doc, a failing or flaky test you can't fix, duplicated logic outside your scope, a security concern, or a decision that needs the owner, **open a GitHub issue**. Mentioning it only in chat, in a code comment or at the end of a long report is not enough.

1. **Search first** so you don't duplicate: `gh issue list --state open --search "<keywords>"`. If one exists, add a comment with your new findings instead.
2. **Create it:**
   ```bash
   gh issue create --title "[BE-10] Short, specific problem" --label "<labels>" --body-file <file>
   ```
   - **Title:** prefix with the task ID, or `[backend]` / `[frontend]` when there's no task.
   - **Labels:** one area (`backend`, `frontend`, `contract`, `docs`, `infra`) and one kind (`bug`, `blocker`, `decision-needed`, `security`, `tech-debt`). Create missing labels with `gh label create`.
   - **Body:** what happened · how to reproduce (commands, inputs) · expected vs actual · files and lines · the doc or ADR section involved · your proposed fix or options · whether it blocks your task.
   - **Security issues:** describe the impact and the location, but don't paste credentials, tokens or a ready-to-use exploit.
3. **Then act on it:**
   - **Blocks your task:** stop that part, leave the PR open, and link the issue in it.
   - **Doesn't block:** continue, and link the issue in your PR and report.
   - **Fixed later:** close it from the fixing PR with `Fixes #<n>`.
4. **List every issue you opened or updated** in your end-of-session report (`docs/AGENT_HANDOFF.md` §5).
5. **If `gh` isn't available or isn't authenticated,** add the same content to `docs/AGENT_HANDOFF.md` §6 and state at the top of your report that the issue still needs to be filed. Never skip raising it.

## 6. Commands

```bash
docker compose up -d db                                   # PostgreSQL on localhost:5432
dotnet build                                              # backend (after BE-01)
dotnet test tests/backend/FlowOps.Domain.Tests            # fast
dotnet test tests/backend/FlowOps.Api.IntegrationTests    # needs Docker
dotnet run --project src/backend/FlowOps.Api              # API on https://localhost:7180
cd src/frontend && npm install && npm run dev             # SPA on http://localhost:5173 (proxies /api, /hubs)
cd src/frontend && npm run typecheck && npm run lint && npm test && npm run build
```

## 7. Conventions in one glance
- Domain vocabulary exactly as `docs/DOMAIN_MODEL.md` §1 (`Request`, `WorkTask`, `Stage`, `Activity`…).
- Error codes `area.snake_case`; DTO inputs `…Request`, outputs `…Dto`; async methods `…Async` with `CancellationToken`.
- Time from `TimeProvider`; money as `Money` (JOD, 3 decimals); dates displayed in `Asia/Amman`.
- Branches `task/<ID>-short-name`: exactly one per task, merged into `main` when done (§4).

## 8. Definition of Done
A task is done only when: its single branch is merged into `main` (§4) · every problem found is raised as a GitHub issue (§5) · the behaviour works end to end · authorization is correct and tested · validation and errors follow the contract · API and frontend types match · required tests pass (and you ran them) · relevant docs are updated · no duplicated business logic was introduced · layers respect their responsibilities · UI follows the design system · `docs/PHASE1_TASKS.md` status is updated · the end-of-session report (`docs/AGENT_HANDOFF.md` §5) is written.
