---
paths:
  - "src/backend/**/*.cs"
  - "src/backend/**/*.csproj"
  - "tests/backend/**/*.cs"
---

# Backend rules (C# / ASP.NET Core)

Read with: `docs/ARCHITECTURE.md` §4, `docs/CODE_STANDARDS.md`, `docs/DOMAIN_MODEL.md`.

## Controllers (`FlowOps.Api/Controllers`)
- `[ApiController]`, route `api/<resource>`, explicit `[ProducesResponseType]` for every success and documented error status (they generate the contract).
- Action = one application-service call + result. No `DbContext`, no `if` on business state, no `try/catch`, no mapping beyond trivial `Ok(dto)` / `CreatedAtAction`.
- Authorization via `[Authorize(Policy = Policies.X)]`; resource checks happen in the service.
- Pass `CancellationToken` from the action.

## Application services (`FlowOps.Application/<Feature>/`)
- Concrete `sealed` classes, primary constructors, registered in `DependencyInjection.cs`.
- Get the actor from `ICurrentUser`; never from parameters that originate in the request body.
- Command flow: load aggregate (tracked) → `AccessRules`/`TaskEligibility` → domain method → `NotificationFactory` → `SaveChangesAsync` **once** → `IRealtimePublisher` after commit.
- Map `DbUpdateConcurrencyException` to `ConflictException(ErrorCodes.ConcurrencyConflict)` in the saving service.
- Queries: `AsNoTracking()` + `Select` into DTOs; filter with `AccessRules.VisibleTo`; page with `ToPagedResultAsync`.
- Services do not call other use-case services.

## Domain (`FlowOps.Domain`)
- Private setters; state changes only via methods that enforce invariants and append activities.
- Time is a parameter (`DateTimeOffset now`), never read from a clock.
- Rule violations: `throw DomainErrors.X()` (wrapping `DomainException(code, message)`), codes from `ErrorCodes`.
- Value objects as `record`s with validating factories (`Money.Create`).

## DTOs
- Inputs `…Request` with DataAnnotations (`[Required]`, `[StringLength]`, `[Range]`); limits come from `RequestLimits` constants.
- Outputs `…Dto` as `record`s; explicit fields only; enums serialize as strings (global `JsonStringEnumConverter`).
- Never expose entities, Identity types, or `xmin` as a number (serialize `version` as string).

## General C#
- Nullable enabled, warnings as errors. `async` all the way; no `.Result`/`.Wait()`.
- Use `TimeProvider`, `Guid.CreateVersion7()`.
- Logging via `ILogger<T>` with message templates (no string interpolation), ids only.
- No AutoMapper, MediatR, generic repositories, static service locators.
