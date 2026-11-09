# ADR-0013: A role-change approver's account must be at least a day older than the proposal

- **Status:** Accepted. The owner delegated the decision on #48 to the agent on 2026-11-09.
- **Date:** 2026-11-09
- **Amends:** ADR-0012 §3 (approval)

## Context
ADR-0012 requires a second administrator to approve an admin's own role change. One admin can still create an Administrator account alone (ADR-0012 §1), sign in to it and approve their own change with it (#48). So one person passes a two-person control.

## Decision
- **Approval rule.** An approver's account must have been created at least **24 hours before the change was proposed**: `approver.CreatedAt <= change.ProposedAt - 24h`. The rule is `RoleChange.IsOldEnoughToApprove`.
- **Approve.** It is checked against the database after the domain checks. Self-approval and not-pending keep their own errors. A too-new approver gets 422 `role_change.approver_too_new`, and nothing changes.
- **Reject.** It needs no minimum age, because refusing a change gains nothing. A too-new admin's `availableActions` is `["reject"]`.
- **Proposal.** "No reviewer" now means "no possible approver": no other active admin old enough (`RoleAssignmentRules.PossibleApprovers`). Such a proposal is refused with 422 `role_change.no_reviewer` instead of waiting until it expires. Every other active admin is still notified, since any of them may reject.
- **No schema change.** `users.created_at` already exists.

## Alternatives considered
- **Accept the risk** (the status quo): the audit log is the only control.
- **`CreatedById` on users** (needs a migration): a third account still gets around it.
- **New admin accounts need approval:** brings the two-person flow back for account creation, which is a larger change.

## Consequences
- \+ Creating a fresh admin account no longer helps an admin approve their own change. An account made before the proposal, more than a day earlier, still can. Its creation is in the audit log, so the misuse needs planning and leaves a trail.
- − **Residual, unchanged from ADR-0012.** One admin can still reset an **existing** second admin's password alone, sign in as them and approve. Both steps are audit-logged. Closing this would need password resets of admins to require approval too. That is a separate decision; it is not taken here.
- − A brand-new administrator can't approve anything proposed before their account was a day old. A company whose only other admin is new waits a day, or another admin edits the roles directly (two people already).
