# ADR-0012: Administrator role only on new accounts; an admin's own role change needs a second admin

- **Status:** Accepted (owner, 2026-11-07, PR #44). The owner set the rules on #32 on 2026-11-06 and 2026-11-07.
- **Date:** 2026-11-06, revised 2026-11-07
- **Phase:** 1 (new tasks BE-08d and FE-13b, before QA-01)

## Context
Administrators have no process authority (AUTHORIZATION.md §2). Since #24, #33 and #41 they can't use admin powers on their **own** account; changing your own roles is refused with 422 `user.self_administration`. #32 asked how to stop admins misusing their powers.

The owner's rules (2026-11-07):
- An administrator can manage everyone, including changing anyone else's roles, alone.
- **No one can be made an administrator by a role change.** The `Administrator` role can only be given when a **new account** is created.
- An administrator **can't change their own roles alone**. This is the only case that needs another administrator's approval. A pending change lasts **72 hours**.

## Decision
1. **What one admin can do alone**, immediate and audit-logged as today:
   - create users with any roles, including `Administrator`, and choose the initial password;
   - change **another** user's roles (`PUT /users/{id}/roles`), as long as the change doesn't add `Administrator` (point 2). This includes removing `Administrator` from another admin, subject to the last-administrator check (owner, 2026-11-07);
   - every `PUT /users/{id}` change, reset another user's password, and every department endpoint.

   The other self rules are unchanged: an admin still can't change their own manager, deactivate themselves or reset their own password through the admin endpoint (422 `user.self_administration`, #24, #33, #41).
2. **`Administrator` only on new accounts.**
   - A `PUT /users/{id}/roles` that would **add** `Administrator` to a user who doesn't already hold it is refused with 422 `user.admin_role_on_create_only`. This applies to any caller and target, including your own pending change (point 3).
   - Keeping `Administrator` in the set for a user who already has it is not "adding" it.
   - The check lives once, in the role validation of `UserAdministrationService`. That validation becomes one private method shared by the immediate path and the approval path (point 3); the rule is not copied.
3. **An admin's own role change needs a second admin.**
   - `PUT /users/{id}/roles` with `id == currentUser.Id` no longer returns 422 `user.self_administration`. It now creates a **pending role change** and returns **202** `RoleChangeDto`. Nothing changes until approval.
   - **No reviewer.** If the caller is the only active administrator, the request is refused with 422 `role_change.no_reviewer` (owner, 2026-11-07). If the other admins are deactivated or demoted after a change is proposed, it simply expires.
   - **Approval.**
     - The approver must be a **different** user who is an active `Administrator` **according to the database**, not the session claims, which can be up to a minute stale (AUTHORIZATION §1.2).
     - It re-runs point 2 and the last-administrator check against current state.
     - It refuses the change with 409 `role_change.stale` if the proposer's current roles no longer equal `PreviousRoles`. Another admin may have changed them directly in the meantime (point 1), and an approval must never silently overwrite that.
     - Everything happens in the approval's single `SaveChangesAsync`.
   - **Two routes, one outcome.** Another admin can also change the admin's roles directly, as today. Point 3 doesn't block that, because it already involves two people. The pending change is for when the admin starts the change themselves.
4. **New aggregate `RoleChange`** in `Domain/Organization`. It is not a `Request`: it is not a business process, and reusing `Request`/`WorkTask` would merge things that only look alike (CODE_STANDARDS §2.2).
   - Fields: `UserId` (the admin whose roles change, who is also the proposer), `ProposedRoles`, `PreviousRoles`, `ProposedAt`, `ExpiresAt` (= `ProposedAt` + 72 h), `Status` (`Pending | Approved | Rejected | Cancelled | Expired`), `DecidedById?`, `DecidedAt?`, `RejectionReason?`, `version` (xmin, ADR-0006).
   - Domain methods take `now` as a parameter (`Approve(approverId, now)`, `Reject(…)`, `Cancel(…)`, `ExpireIfDue(now)`).
   - Invariants:
     - the decider ≠ `UserId` for approve and reject; only `UserId` may cancel;
     - only `Pending` and not-expired changes can be decided;
     - at most one `Pending` change per user (partial unique index).
5. **Expiry: 72 hours, without a background job** (ADR-0010).
   - Reads compute "expired" from `ExpiresAt <= now`, so `?status=Pending` excludes expired rows.
   - Deciding an expired change returns 409 `role_change.not_pending`; it reads as `Expired` with `decidedAt = expiresAt`.
   - A new proposal first marks the user's expired `Pending` row as `Expired` in the same transaction, so the unique index doesn't block it. A live pending change returns 409 `role_change.already_pending`.
6. **API** (contract change, AGENT_HANDOFF §3):
   - `PUT /users/{id}/roles`:
     - 200 `UserDto` for another user, as today;
     - **202** `RoleChangeDto` for your own roles;
     - 422 `user.admin_role_on_create_only`;
     - 422 `role_change.no_reviewer`;
     - 409 `role_change.already_pending`;
     - plus the existing 404, 409 `concurrency.conflict` and 422 `user.last_admin`.
   - `GET /api/admin/role-changes?status=&page=&pageSize=` (Admin policy) returns paged `RoleChangeDto`, and `GET /api/admin/role-changes/{id}` one of them (the 202's `Location`). Every admin sees every role change.
   - `POST /api/admin/role-changes/{id}/approve` `{ version }` returns 200 `RoleChangeDto`. Errors: 422 `role_change.self_approval`, 409 `role_change.not_pending`, 409 `role_change.stale`, 409 `concurrency.conflict`, and 422 `user.last_admin` / `user.admin_role_on_create_only` when re-validation fails.
   - `POST /api/admin/role-changes/{id}/reject` `{ version, reason }` returns 200, with the same `self_approval` / `not_pending` / `concurrency` errors.
   - `POST /api/admin/role-changes/{id}/cancel` `{ version }`, proposer only, returns 200. 403 `role_change.not_proposer`.
   - `RoleChangeDto`: `{ id, user: UserRef, proposedRoles, previousRoles, status, proposedAt, expiresAt, decidedBy: UserRef | null, decidedAt, rejectionReason, version, availableActions: ("approve" | "reject" | "cancel")[] }`.
   - All new error codes go into `ErrorCodes` + `ErrorStatusCodes` and the frontend message map (`problem.test.ts` enforces this).
7. **Notifications.**
   - A new type `RoleChangePending` goes to every other active admin, through `NotificationFactory`, in the same transaction (ADR-0009).
   - Approval or rejection notifies the proposer (`RoleChangeApproved` / `RoleChangeRejected`).
   - Notifications and logs carry ids and the status only. The rejection reason is shown only in `RoleChangeDto`, never logged.
8. **Frontend (FE-13b).**
   - The edit-roles form for an existing user doesn't offer to *add* `Administrator`; it can only keep it or remove it. This mirrors the contract rule in point 2, like the limits in `lib/api/limits.ts`; it is not a permission check. The server enforces the rule with 422 `user.admin_role_on_create_only`. The create form still offers `Administrator`.
   - Editing your own roles shows "Waiting for another administrator" after the 202.
   - A "Role changes" list (pending / decided) shows approve, reject and cancel from `availableActions`, and handles 409 with the mapped message plus a refetch.
9. **Docs to update with the implementation:**
   - AUTHORIZATION §2 (the sentence on own roles) and the §4 matrix row;
   - API_CONTRACT §8, plus the new role-changes section and its error codes;
   - DOMAIN_MODEL (the `RoleChange` aggregate);
   - the notification types;
   - UI_SPEC §4.8;
   - PHASE1_TASKS (BE-08d, FE-13b).

## Consequences
- \+ No admin can give themselves process roles alone, and no existing account can be promoted to administrator.
- \+ Small scope: one aggregate, one migration, 4 new endpoints, a 202 on one existing endpoint for one case, and new 4xx codes. Create, edit and reset password keep today's contract.
- − **Residual risks accepted by the owner.** One admin can still, alone and with only the audit log as control:
  - grant process roles to another account they control;
  - create a new administrator account with a password they know;
  - reset another user's password and sign in as them;
  - remove `Administrator` from every other admin. They then become the only admin and can't change their own roles until they create another admin account.
- − `Administrator` can't be restored to a user who lost it; they need a new account.
- − A one-admin organisation can't change its admin's own roles until it creates a second administrator.
- − The concurrent-demotion race on the last-administrator check (accepted in BE-08) also applies to approvals.

## Alternatives rejected
- **Audit log only** (status quo): detects, doesn't prevent.
- **Keep refusing self role changes and let another admin make them directly** (today's behaviour): it is already two-person. The owner chose a pending-change flow so the admin can start the change themselves and it waits for approval.
- **Two-person control for creates, manager changes, reactivation, password resets and all role changes** (earlier drafts): the owner chose one-admin control for everything except an admin's own role change.
- **Reuse `Request`/`FixedProcesses` as a third request type:** it would give admins process authority and tasks (against ADR-0003 and AUTHORIZATION §2).
