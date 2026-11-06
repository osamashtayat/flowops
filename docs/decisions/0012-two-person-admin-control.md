# ADR-0012: Two-person control for administrative changes that grant authority

- **Status:** Proposed (owner chose this direction on #32, 2026-11-06; needs acceptance of the details below before implementation)
- **Date:** 2026-11-06

## Context
Administrators have no process authority (AUTHORIZATION.md §2), and since #24, #33 and #41 they can't use admin powers on their **own** account. One admin can still get authority through **another** account (#32):
- create a user with a process role (or as someone's manager) and an initial password they choose, then sign in as it (`MustChangePassword` doesn't help, because they pick the next password too);
- reset someone else's password, then sign in as them;
- give an accomplice account a role or a manager link.

Today the only control is the audit log (every change is logged with `ActorId`). The owner chose two-person control: a second administrator has to approve these changes before they take effect.

## Decision
1. **Covered changes ("authority changes")** are *proposed* by one admin and *applied* only after a **different, active** admin approves them:
   - creating a user (every new account comes with an initial password the creator knows);
   - `PUT /users/{id}/roles` (any change to roles);
   - `PUT /users/{id}` when it changes `managerId` or reactivates a user (`isActive` false → true);
   - `POST /users/{id}/reset-password`.

   These stay **immediate**: display name and department edits, deactivation (it removes access), and every department endpoint.
2. **New aggregate `AdminChange`** in `Domain/Organization`, not a `Request`. It is not a business process, and reusing `Request`/`WorkTask` would merge two things that only look alike (CODE_STANDARDS §2.2) and drag ADR-0004 in.
   - Fields: `Kind` (`CreateUser | ReplaceRoles | UpdateManagerOrReactivate | ResetPassword`), `TargetUserId?`, a typed payload (jsonb), `ProposedById`, `ProposedAt`, `Status` (`Pending | Approved | Rejected | Cancelled | Expired`), `DecidedById?`, `DecidedAt?`, `version` (xmin, ADR-0006).
   - Invariants:
     - the approver ≠ the proposer;
     - the approver is not the target user;
     - only `Pending` can be decided;
     - only one pending change per target user and kind (partial unique index).
   - Applying the change re-runs the existing rules (`UserAdministrationService` checks: self-administration, last admin, manager cycle, inactive department) **at approval time**, against current state, in the approval's single `SaveChangesAsync`.
3. **Passwords in pending changes.**
   - A pending `CreateUser` or `ResetPassword` change never stores the plaintext. The proposer supplies none.
   - On approval the server generates a one-time initial password and returns it **only to the approver** in the approval response, never logged. Neither admin alone knows a password the other set. The approver hands it over out of band.
   - The user must change it on first sign-in (as today).
4. **Expiry.** Pending changes expire after 72 hours. No background job (ADR-0010): expiry is evaluated when a change is read or decided.
5. **API** (contract change, AGENT_HANDOFF §3):
   - The covered endpoints return **202** `AdminChangeDto` instead of 200/201/204 when the change is pending.
   - `GET /api/admin/changes?status=` (paged) lists them.
   - `POST /api/admin/changes/{id}/approve` applies the change: 200 `AdminChangeDto`, plus `initialPassword` when one was generated.
   - `POST /api/admin/changes/{id}/reject` (with a reason) and `POST /api/admin/changes/{id}/cancel` (proposer only).
   - Errors:
     - 422 `admin_change.self_approval` (approver is the proposer or the target);
     - 409 `admin_change.not_pending`;
     - 409 `concurrency.conflict`;
     - 422 with the underlying rule's code when re-validation fails.
6. **Notifications.** A new type `AdminChangePending` goes to every other active admin, through `NotificationFactory`, in the same transaction (ADR-0009). Decisions notify the proposer. The text carries no passwords and no role details beyond the kind.
7. **Fewer than two active admins.** Covered changes can't be applied. The one exception is creating the **second** administrator: it is applied immediately and flagged `unreviewed` in the audit log, so the bootstrap admin isn't locked out. Deactivating or demoting an admin is refused (422 `user.last_reviewer`) if it would leave changes pending with nobody able to approve them.
8. **Frontend.** An "Admin changes" screen (pending / decided) with approve/reject, built from `availableActions` on `AdminChangeDto`. The user form shows "Waiting for another administrator" after a 202. The one-time password is shown once to the approver, with a copy button and no persistence.

## Consequences
- \+ No single administrator can create, empower or take over an account alone; collusion needs two admins, and both are in the audit log.
- \+ Existing rules aren't duplicated: approval reuses `UserAdministrationService`'s checks.
- − Every new hire needs two admins. A one-admin organisation can only add its second admin, then must work in pairs.
- − It is a sizeable Phase 1 addition: a new aggregate, a migration, 4 endpoints, a status-code change on 4 existing endpoints, notifications and a frontend screen. It replaces the immediate behaviour that FE-13 is built on.
- − Initial passwords change from "chosen by the creator" to "generated at approval" (contract change on `POST /users`: `initialPassword` is dropped from the request).

## Alternatives rejected
- **Audit log only** (status quo): detects, doesn't prevent.
- **Forbid resetting passwords of users with roles** (option 2 on #32): leaves "create a sock-puppet with a known password" open.
- **Reuse `Request`/`FixedProcesses` as a third request type:** it would give admins process authority and tasks (against ADR-0003 and AUTHORIZATION §2) and couple user administration to the business process engine.
- **Email-delivered set-password links instead of generated passwords:** better, but Phase 1 has no outgoing email.

## Open questions for the owner
1. Is this in **Phase 1** (new tasks BE-08d + FE-13b before QA-01), or Phase 2?
2. Is the covered list right? In particular: should creating an **Employee-only** user also need approval? (Recommended yes: the sock-puppet risk comes from the known password, not the role.)
3. Is 72-hour expiry acceptable?
4. Is the two-admin bootstrap exception (point 7) acceptable?
