# Security rules (always apply)

Full design: `docs/AUTHORIZATION.md`, ADR-0003.

## Identity of the caller
- The actor is always `ICurrentUser` (server-side claims). Never accept `userId`, `requesterId`, `departmentId`, `role`, `ownerId` or similar from a request body/query to decide *who* is acting or *what they own*.

## Authorization
- Every endpoint is authenticated by default (fallback policy); `[AllowAnonymous]` only on `POST /api/auth/login` and `GET /api/health`.
- Coarse checks: named policies (`Policies.*`) on controllers. Resource checks: **only** `AccessRules` (visibility) and `TaskEligibility` (decisions/available actions). Never write a parallel check elsewhere.
- Not visible → 404. Visible but forbidden → 403 with a `code`. Unauthenticated → 401 ProblemDetails (never a redirect).
- Self-decision is always forbidden. Administrator role grants no request visibility or decision authority.
- Hiding a button is not authorization. The frontend renders from server-provided `capabilities` / `availableActions`.

## Input & output
- Validate input at the API boundary (DataAnnotations) and invariants in the domain. Frontend validation is UX only.
- Never return entities; DTOs list fields explicitly. No password hashes, security stamps, lockout data, or internal flags.
- Leave `reason` only to requester, direct manager, HR (see AUTHORIZATION.md §5). Never in notifications or logs.
- React: no `dangerouslySetInnerHTML`; all user text is plain text.

## Session & CSRF
- Cookie: `__Host-`, HttpOnly, Secure, SameSite=Strict. Antiforgery header (`X-XSRF-TOKEN`) required on unsafe methods. No CORS.

## Secrets
- No secrets in source, appsettings committed files, or Docker images. Use environment variables / `dotnet user-secrets`; `.env` is gitignored, `.env.example` holds placeholders only.

## Logging
- Log ids, outcomes, error codes, trace ids. Never request bodies, comments, reasons, passwords, cookies, tokens.

## Concurrency as security
- Decisions must carry and check `version`; conflicts → 409. Never "fix" a conflict by retrying a decision silently.
