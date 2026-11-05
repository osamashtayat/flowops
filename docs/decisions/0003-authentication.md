# ADR-0003: Cookie authentication with ASP.NET Core Identity; resource checks in Application

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
The SPA is first-party and same-origin. We must address XSS token theft, CSRF, revocation and simplicity. Resource authorization (ownership, task assignment) must be centralised and usable for both list filtering and single checks.

## Decision
ASP.NET Core Identity with an HttpOnly, Secure, SameSite=Strict `__Host-` cookie; antiforgery header on unsafe methods; no CORS; 401/403 as ProblemDetails. No public registration; bootstrap admin from configuration; admin-created accounts with forced password change. Role/coarse checks via ASP.NET policies on controllers; resource checks via `AccessRules`/`TaskEligibility` in Application (not `IAuthorizationHandler`), returning 404 for invisible and 403 for not-permitted. "Manager" is derived from the reporting relationship, not a role. Details: AUTHORIZATION.md.

## Consequences
+ Tokens unreadable by JS; server-side logout/revocation; no refresh-token machinery.
+ One implementation of visibility shared by lists and details.
- Requires same-origin deployment (reverse proxy) and antiforgery handling in the client.
- Non-browser clients need a different scheme later (Phase 3).

## Alternatives rejected
JWT in localStorage (XSS exposure); JWT in memory + refresh cookie (complexity without benefit here); `IAuthorizationHandler` resource handlers (force controllers to load entities); a Manager role (can disagree with the reporting line).
