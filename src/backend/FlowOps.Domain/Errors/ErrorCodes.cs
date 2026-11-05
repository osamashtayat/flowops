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
    public const string AuthLockedOut = "auth.locked_out";

    public const string TaskNotEligible = "task.not_eligible";
    public const string TaskSelfDecision = "task.self_decision";
    public const string TaskAlreadyDecided = "task.already_decided";
    public const string TaskNotCurrent = "task.not_current";
    public const string TaskInvalidAction = "task.invalid_action";

    public const string RequestClosed = "request.closed";
    public const string RequestManagerNotAssigned = "request.manager_not_assigned";
    public const string RequestDepartmentNotAssigned = "request.department_not_assigned";

    public const string DepartmentDuplicate = "department.duplicate";
    public const string DepartmentInactive = "department.inactive";

    public const string UserDuplicateEmail = "user.duplicate_email";
    public const string UserManagerCycle = "user.manager_cycle";
    public const string UserLastAdmin = "user.last_admin";
}
