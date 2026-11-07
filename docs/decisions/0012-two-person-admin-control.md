# ADR-0012: Administrator role only on new accounts; an admin's own role change needs a second admin

- **Status:** Proposed (owner chose two-person control on #32 on 2026-11-06 and set its scope on 2026-11-07. The details below need acceptance before implementation.)
- **Date:** 2026-11-06, revised 2026-11-07

## Context
Administrators have no process authority (AUTHORIZATION.md §2). Since #24, #33 and #41 they can't use admin powers on their **own** account; changing your own roles is refused with 422 `user.self_administration`. #32 asked how to stop admins misusing their powers.

The owner's rules (2026-11-07):
- An administrator can manage everyone, including changing anyone's roles, alone.
- **No one can be made an administrator by a role change.** The `Administrator` role can only be given when a **new account** is created.
- An administrator **can't change their own roles alone**. Only in this case another administrator must approve. A pending change lasts **72 hours**.

## Decision
1. **What one admin can do alone**, immediate and audit-logged as today:
   - create users with any roles, including `Administrator`, and choose the initial password;
   - change **another** user's roles (`PUT /users/{id}/roles`), as long as the change doesn't add `Administrator` (point 2);
   - every `PUT /users/{id}` change, reset another user's password, and every department endpoint.

   The existing rules still apply: the last-administrator check, and the self rules on your own manager, deactivation and password (#24, #33, #41).
2. **`Administrator` only on new accounts.**
   - `PUT /users/{id}/roles` that would **add** `Administrator` to an existing user is refused with 422 `user.admin_role_on_create_only`. This applies to anyone, including a self change (point 3).
   - Removing `Administrator` from another user is allowed alone, subject to the last-administrator check.
   - The rule lives in one place, the role-change validation in `UserAdministrationService`. Its error code goes into `ErrorCodes` and the frontend message map.
3. **An admin's own role change needs a second admin.**
   - Today `PUT /users/{id}/roles` with `id == currentUser.Id` returns 422 `user.self_administration`. Instead it now creates a **pending role change** and returns **202** `RoleChangeDto`. Nothing changes until approval.
   - A **different, active** administrator approves or rejects it.
   - Approval re-runs the same validation as an immediate change (point 2, last administrator, active user) against the state at that moment. It runs in the approval's single `SaveChangesAsync`. The rules are called, not copied.
   - If the proposer is the only active administrator, the proposal is refused with 422 `role_change.no_reviewer`, because nobody could approve it.
4. **New aggregate `RoleChange`** in `Domain/Organization`. It is not a `Request`: it is not a business process, and reusing `Request`/`WorkTask` would merge things that only look alike (CODE_STANDARDS §2.2).
   - Fields: `TargetUserId` (always the proposer, for now), `ProposedRoles`, `PreviousRoles`, `ProposedById`, `ProposedAt`, `ExpiresAt` (= `ProposedAt` + 72 h), `Status` (`Pending | Approved | Rejected | Cancelled | Expired`), `DecidedById?`, `DecidedAt?`, `RejectionReason?`, `version` (xmin, ADR-0006).
   - Invariants:
     - the approver ≠ the proposer;
     - only `Pending` and not-yet-expired changes can be decided;
     - at most one pending change per user (partial unique index).
5. **Expiry: 72 hours.** No background job (ADR-0010). A change past `ExpiresAt` is treated as `Expired` when it is read or decided, and deciding it returns 409 `role_change.not_pending`.
6. **API** (contract change, AGENT_HANDOFF §3):
   - `PUT /users/{id}/roles`:
     - 200 as today for another user;
     - **202** `RoleChangeDto` for your own roles;
     - 422 `user.admin_role_on_create_only`;
     - 422 `role_change.no_reviewer`;
     - 409 `role_change.already_pending`.
   - `GET /api/admin/role-changes?status=` (paged).
   - `POST /api/admin/role-changes/{id}/approve` returns 200 `RoleChangeDto`. 422 `role_change.self_approval` if the caller is the proposer.
   - `POST /api/admin/role-changes/{id}/reject` (with a reason).
   - `POST /api/admin/role-changes/{id}/cancel` (proposer only).
   - Shared errors: 409 `role_change.not_pending`, 409 `concurrency.conflict`, and 422 with the underlying rule's code when re-validation fails.
   - `user.self_administration` stays for the other self actions (own manager, deactivation, password reset).
7. **Notifications.** A new type `RoleChangePending` goes to every other active admin, through `NotificationFactory`, in the same transaction (ADR-0009). Approval or rejection notifies the proposer.
8. **Frontend.**
   - The roles editor no longer offers `Administrator` when editing an existing user. That comes from the server's options, not role-name logic in the UI. The create form still offers it.
   - Editing your own roles shows "Waiting for another administrator" after the 202.
   - A small "Role changes" list (pending / decided) offers approve and reject from `availableActions` on `RoleChangeDto`.

## Consequences
- \+ No admin can quietly give themselves process roles. No existing account (for example an accomplice's) can be promoted to administrator.
- \+ Small scope: one aggregate, one migration, 4 new endpoints, a 202 on one existing endpoint for one case, and a new 422. Create, edit and reset password keep today's contract.
- − **Residual risks accepted by the owner.** One admin can still, alone and with only the audit log as control:
  - grant process roles to another account they control;
  - create a new administrator account with a password they know;
  - reset another user's password and sign in as them.
- − Restoring `Administrator` to someone who lost it means creating a new account for them.
- − A one-admin organisation can't change its admin's own roles until it creates a second administrator.

## Alternatives rejected
- **Audit log only** (status quo): detects, doesn't prevent.
- **Two-person control for creates, manager changes, reactivation, password resets and all role changes** (earlier drafts): the owner chose one-admin control for everything except an admin's own role change.
- **Reuse `Request`/`FixedProcesses` as a third request type:** it would give admins process authority and tasks (against ADR-0003 and AUTHORIZATION §2).

## Open questions for the owner
1. Is this in **Phase 1** (new tasks BE-08d + FE-13b before QA-01), or Phase 2?
2. Can an admin *remove* `Administrator` from **another** admin alone (as drafted), or should that also need approval?
3. A self role change by the only admin is refused (`role_change.no_reviewer`). Is that right, or should it stay pending and expire?
