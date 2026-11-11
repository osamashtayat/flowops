namespace FlowOps.Domain.Errors;

/// <summary>
/// Every stable, machine-readable error code (docs/API_CONTRACT.md §2).
/// Adding a code means updating this file, that table and src/frontend/src/lib/api/problem.ts.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation";
    public const string NotFound = "not_found";
    public const string ServerError = "server.error";
    public const string ConcurrencyConflict = "concurrency.conflict";

    public const string AuthUnauthenticated = "auth.unauthenticated";
    public const string AuthForbidden = "auth.forbidden";
    public const string AuthPasswordChangeRequired = "auth.password_change_required";
    public const string AuthInvalidCredentials = "auth.invalid_credentials";
    public const string AuthRateLimited = "auth.rate_limited";

    public const string TaskNotEligible = "task.not_eligible";
    public const string TaskSelfDecision = "task.self_decision";
    public const string TaskAlreadyParticipated = "task.already_participated";
    public const string TaskAlreadyDecided = "task.already_decided";
    public const string TaskNotCurrent = "task.not_current";
    public const string TaskInvalidAction = "task.invalid_action";

    public const string RequestClosed = "request.closed";
    public const string RequestManagerNotAssigned = "request.manager_not_assigned";
    public const string RequestDepartmentNotAssigned = "request.department_not_assigned";
    public const string RequestCommentRateLimited = "request.comment_rate_limited";

    public const string DepartmentDuplicate = "department.duplicate";
    public const string DepartmentInactive = "department.inactive";

    public const string UserDuplicateEmail = "user.duplicate_email";
    public const string UserManagerCycle = "user.manager_cycle";
    public const string UserLastAdmin = "user.last_admin";
    public const string UserSelfAdministration = "user.self_administration";
    public const string UserAdminRoleOnCreateOnly = "user.admin_role_on_create_only";

    public const string RoleChangeNoReviewer = "role_change.no_reviewer";
    public const string RoleChangeApproverTooNew = "role_change.approver_too_new";
    public const string RoleChangeAlreadyPending = "role_change.already_pending";
    public const string RoleChangeSelfApproval = "role_change.self_approval";
    public const string RoleChangeNotPending = "role_change.not_pending";
    public const string RoleChangeStale = "role_change.stale";
    public const string RoleChangeNotProposer = "role_change.not_proposer";

    public const string WorkflowDraftExists = "workflow.draft_exists";
    public const string WorkflowVersionNotDraft = "workflow.version_not_draft";
    public const string WorkflowInvalid = "workflow.invalid";
}
