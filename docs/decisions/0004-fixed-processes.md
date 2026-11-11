# ADR-0004: Fixed processes in code; no workflow engine in Phase 1

- **Status:** Superseded by ADR-0014 (2026-11-11) for Phase 2; in force until Phase 2 replaces `FixedProcesses`
- **Date:** 2026-11-05

## Context
Phase 1 needs two processes. Phase 2 will add configurable workflows. Building an engine now risks speculative abstraction; hard-coding per-type if-chains risks a rewrite later.

## Decision
Processes are declared as ordered `ProcessStep(Stage, Kind, AssigneeRule)` lists in `FixedProcesses`. The `Request` aggregate advances through them generically. No JSON definitions, no interpreter, no designer. Adding a request type touches FixedProcesses, a details type, a submit endpoint and a form — not decision, access, inbox or notification code.

## Consequences
+ Behaviour is explicit and easy to test.
+ The aggregate's advancement logic is already data-driven, which is exactly the seam Phase 2 needs.
- Changing a process requires a deploy (acceptable in Phase 1).

## Alternatives rejected
Embedding a BPMN engine (Elsa, Camunda) — heavy for two linear processes; per-type services with if/else (duplicated approval logic, rewrite in Phase 2).
