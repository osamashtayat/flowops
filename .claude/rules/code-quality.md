# Code quality rules (always apply)

Full guidance with examples: `docs/CODE_STANDARDS.md`.

## DRY — one home per piece of knowledge
Before writing logic, check `docs/CODE_STANDARDS.md` §2.3 for its existing home. Reuse it.
- Extract when the same **rule or behaviour with the same meaning** appears twice, or when changing one copy would force changing the other.
- Don't extract code that merely looks similar but changes for different reasons; don't add flag parameters to force-fit two callers; don't abstract for a hypothetical second caller.
- Found duplication outside your task's scope? Report it with file paths instead of refactoring.

## Separation of concerns & SOLID (pragmatic)
- One reason to change per class/component/function.
- Composition over inheritance; `sealed` C# classes by default.
- Small focused ports (`IRealtimePublisher`), not `IService<T>`.
- Concrete services registered directly; no single-implementation interfaces without a boundary reason.

## Size & shape
- Methods ≲ 30 lines, nesting ≤ 3; guard clauses and `switch` expressions over if-ladders.
- React components ≲ 150 lines; split fetching, layout and dialogs into parts when they grow.
- No god classes, no `Utils`/`Helpers` dumping grounds — name helpers for the one thing they do.

## Naming
- Domain vocabulary exactly as `docs/DOMAIN_MODEL.md` §1.
- `…Request` input DTOs, `…Dto` outputs; `…Async` + `CancellationToken`; booleans `is/has/can`.
- No magic strings: roles, policies, error codes, query keys, claim types are constants.

## Errors
- Throw typed exceptions; global handler maps to ProblemDetails. No try/catch in controllers. Never swallow exceptions. No `catch (e: any)` in TS.

## Changes
- Keep diffs focused on the task. No drive-by refactors or formatting churn.
- Update the docs your change affects in the same change.
- Comments explain *why*, not *what*.

## Review standard (apply to your own work before reporting)
Run the checklist in `docs/CODE_STANDARDS.md` §12.
