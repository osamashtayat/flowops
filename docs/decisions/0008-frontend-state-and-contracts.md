# ADR-0008: TanStack Query for server state; OpenAPI-generated types; contract-first skeleton

- **Status:** Accepted
- **Date:** 2026-11-05

## Context
Frontend and backend are built by separate sessions. Hand-written frontend types drift; ad-hoc fetching duplicates caching and error handling.

## Decision
Backend publishes `contracts/openapi.json` (build-time generation, committed, CI drift check), starting with a full endpoint skeleton (BE-03). Frontend generates `schema.d.ts` with `openapi-typescript` and calls the API only through `openapi-fetch` in `lib/api/client.ts`. Server state lives only in TanStack Query with central query keys and feature hooks. Client state: React state/Context for session only; no Redux/Zustand. Forms: react-hook-form + zod. Tests mock the network with MSW handlers typed from the schema.

## Consequences
+ Type errors reveal contract drift at compile time.
+ Frontend can build features in parallel against the skeleton.
- zod schemas duplicate validation limits (bridged by a single `limits.ts` + contract doc; acceptable).
- Generated types depend on accurate `[ProducesResponseType]`/nullability annotations on the backend.

## Alternatives rejected
Orval/full generated React Query hooks (more generated surface, less control over keys/invalidation); GraphQL (no need); hand-written types (drift).
