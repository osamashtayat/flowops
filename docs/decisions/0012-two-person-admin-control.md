# ADR-0012: Two-person control for role changes

- **Status:** Proposed (owner chose two-person control on #32, 2026-11-06; on 2026-11-07 the owner narrowed it to role changes only. The details below need acceptance before implementation.)
- **Date:** 2026-11-06, revised 2026-11-07

## Context
Administrators have no process authority (AUTHORIZATION.md §2). Since #24, #33 and #41 they also can't use admin powers on their **own** account. One admin can still give authority through **another** account (#32), and the strongest form of that is granting process roles (`Manager`, `FinanceOfficer`, `HrOfficer`, `ProcurementOfficer`) or `Administrator` to an accomplice account.

Today the only control is the audit log: every change is logged with `ActorId`. The owner chose two-person control, then narrowed it. A second administrator must approve **role changes**. Every other administrative action stays with one admin.

## Decision
1. **Scope.** Only role changes are covered. A role change is *proposed* by one admin and *applied* only after a **different, active** admin approves it. A role change means:
   - `PUT /users/{id}/roles` (any change to the role set);
   - `POST /users` when it asks for any role other than `Employee` (point 3).

   Everything else stays **immediate**, with a single admin and audit-logged as today:
   - creating users with `Employee` only, including choosing the initial password;
   - `PUT /users/{id}`, including `managerId` changes, reactivation, deactivation, display name and department;
   - `POST /users/{id}/reset-password`;
   - every department endpoint.

   The self-administration rules (#24, #33, #41) still apply to these immediate actions.
2. **New aggregate `RoleChange`** in `Domain/Organization`. It is not a `Request`. It is not a business process, and reusing `Request`/`WorkTask` would merge two things that only look alike (CODE_STANDARDS §2.2) and drag ADR-0004 in. It is named for the one thing it does; it is not a generic "admin change" engine for a second caller that doesn't exist.
   - Fields: `TargetUserId`, `ProposedRoles` (the full new set), `PreviousRoles` (snapshot, for the audit log), `ProposedById`, `ProposedAt`, `Status` (`Pending | Approved | Rejected | Cancelled | Expired`), `DecidedById?`, `DecidedAt?`, `RejectionReason?`, `version` (xmin, ADR-0006).
   - Invariants:
     - the approver ≠ the proposer;
     - the approver ≠ the target user;
     - only `Pending` can be decided;
     - at most one pending change per target user (partial unique index).
   - Approval re-runs the existing role rules from `UserAdministrationService` (self-administration, last administrator, inactive target) **at approval time**, against current state, in the approval's single `SaveChangesAsync`. Those rules move into a helper that never saves, so both the proposal and the approval call it. They are not copied.
3. **Creating a user with elevated roles.** `POST /users` creates the user **immediately** with `Employee` only, so onboarding isn't blocked. If the request asked for more roles, the same transaction adds a pending `RoleChange` for the rest. The response stays 201 `UserDto` (roles = `[Employee]`) and gains an optional `pendingRoleChange: RoleChangeDto`.
4. **Expiry.** Pending changes expire after 72 hours. There is no background job (ADR-0010): expiry is evaluated when a change is read or decided.
5. **API** (contract change, AGENT_HANDOFF §3):
   - `PUT /users/{id}/roles` returns **202** `RoleChangeDto` instead of 200. The roles don't change until approval.
   - `GET /api/admin/role-changes?status=` (paged) lists them.
   - `POST /api/admin/role-changes/{id}/approve` applies the change and returns 200 `RoleChangeDto`.
   - `POST /api/admin/role-changes/{id}/reject` (with a reason).
   - `POST /api/admin/role-changes/{id}/cancel` (proposer only).
   - Errors:
     - 422 `role_change.self_approval` when the approver is the proposer or the target;
     - 409 `role_change.not_pending`;
     - 409 `role_change.already_pending` when a second proposal is made for the same user;
     - 409 `concurrency.conflict`;
     - 422 with the underlying rule's code when re-validation fails.
6. **Notifications.** A new type `RoleChangePending` goes to every other active admin, through `NotificationFactory`, in the same transaction (ADR-0009). Decisions notify the proposer. The text names the target user and the kind of change only.
7. **Fewer than two active admins.** Role changes can't be applied, with one exception: granting `Administrator` to create the **second** admin is applied immediately and flagged `unreviewed` in the audit log, so the bootstrap admin isn't locked out. Deactivating or demoting an admin is refused (422 `user.last_reviewer`) if it would leave changes pending with nobody able to approve them.
8. **Frontend.**
   - A "Role changes" screen (pending / decided) with approve and reject, built from `availableActions` on `RoleChangeDto`.
   - After a 202 the roles editor shows "Waiting for another administrator", and after a create the user form shows the same message.
   - The rest of FE-13 (create, edit, reset password) is unchanged.

## Consequences
- \+ No single administrator can grant process authority or another admin seat. Collusion needs two admins, and both are in the audit log.
- \+ The change is much smaller than the first draft: one aggregate, one migration, 4 new endpoints and one status-code change. Creation, edits and password resets keep today's contract and FE-13's behaviour.
- \+ Existing rules aren't duplicated: approval reuses the role checks.
- − **Accepted residual risk (owner's choice).** One admin can still get authority on their own in three ways, and the audit log is the only control for them:
  - reset the password of a user who already holds a role, then sign in as them;
  - create an `Employee` account with a known password and sign in as it (an employee can request but not decide, so the gain is small);
  - set a `managerId` so that an account they control becomes someone's direct manager, which gives it decision authority over that person's requests.
- − A one-admin organisation can only add its second admin; after that, role changes need two.

## Alternatives rejected
- **Audit log only** (status quo): detects, doesn't prevent.
- **Two-person control for creates, manager changes, reactivation and password resets as well** (first draft of this ADR): the owner chose to keep those with one admin. That draft also needed passwords generated at approval and a contract change on `POST /users`.
- **Forbid resetting passwords of users with roles** (option 2 on #32): not chosen.
- **Reuse `Request`/`FixedProcesses` as a third request type:** it would give admins process authority and tasks (against ADR-0003 and AUTHORIZATION §2) and couple user administration to the business process engine.

## Open questions for the owner
1. Is this in **Phase 1** (new tasks BE-08d + FE-13b before QA-01), or Phase 2?
2. Creating a user with elevated roles (point 3): create them as `Employee` with a pending role change (recommended), or refuse non-`Employee` roles on `POST /users` with 422 so the admin must make a separate role change?
3. Is 72-hour expiry acceptable?
4. Is the two-admin bootstrap exception (point 7) acceptable?
5. Do you accept the residual risks listed under Consequences, especially manager links? If not, `managerId` changes are the cheapest one to add back under two-person control.
