using FlowOps.Domain.Errors;

namespace FlowOps.Api.Errors;

/// <summary>
/// The single code → HTTP status table (API_CONTRACT.md §2). The status is derived from the error
/// code, never from the exception type, so Domain and Application failures map identically.
/// </summary>
public static class ErrorStatusCodes
{
    private static readonly Dictionary<string, int> StatusByCode = new(StringComparer.Ordinal)
    {
        [ErrorCodes.Validation] = StatusCodes.Status400BadRequest,

        [ErrorCodes.AuthUnauthenticated] = StatusCodes.Status401Unauthorized,
        [ErrorCodes.AuthInvalidCredentials] = StatusCodes.Status401Unauthorized,

        [ErrorCodes.AuthForbidden] = StatusCodes.Status403Forbidden,
        [ErrorCodes.AuthPasswordChangeRequired] = StatusCodes.Status403Forbidden,
        [ErrorCodes.TaskNotEligible] = StatusCodes.Status403Forbidden,
        [ErrorCodes.TaskSelfDecision] = StatusCodes.Status403Forbidden,
        [ErrorCodes.RoleChangeNotProposer] = StatusCodes.Status403Forbidden,

        [ErrorCodes.NotFound] = StatusCodes.Status404NotFound,

        [ErrorCodes.TaskAlreadyDecided] = StatusCodes.Status409Conflict,
        [ErrorCodes.TaskNotCurrent] = StatusCodes.Status409Conflict,
        [ErrorCodes.TaskInvalidAction] = StatusCodes.Status409Conflict,
        [ErrorCodes.RequestClosed] = StatusCodes.Status409Conflict,
        [ErrorCodes.ConcurrencyConflict] = StatusCodes.Status409Conflict,
        [ErrorCodes.DepartmentDuplicate] = StatusCodes.Status409Conflict,
        [ErrorCodes.UserDuplicateEmail] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoleChangeAlreadyPending] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoleChangeNotPending] = StatusCodes.Status409Conflict,
        [ErrorCodes.RoleChangeStale] = StatusCodes.Status409Conflict,

        [ErrorCodes.RequestManagerNotAssigned] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RequestDepartmentNotAssigned] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UserManagerCycle] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UserLastAdmin] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UserSelfAdministration] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.DepartmentInactive] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.UserAdminRoleOnCreateOnly] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RoleChangeNoReviewer] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RoleChangeApproverTooNew] = StatusCodes.Status422UnprocessableEntity,
        [ErrorCodes.RoleChangeSelfApproval] = StatusCodes.Status422UnprocessableEntity,

        [ErrorCodes.AuthRateLimited] = StatusCodes.Status429TooManyRequests,
        [ErrorCodes.RequestCommentRateLimited] = StatusCodes.Status429TooManyRequests,

        [ErrorCodes.ServerError] = StatusCodes.Status500InternalServerError,
    };

    /// <summary>Every code the table knows; a test asserts this equals the full <see cref="ErrorCodes"/> list.</summary>
    public static IReadOnlyCollection<string> MappedCodes => StatusByCode.Keys;

    /// <summary>Unknown codes are a programming error and surface as 500, never as a guessed 4xx.</summary>
    public static int For(string code) =>
        StatusByCode.TryGetValue(code, out var status) ? status : StatusCodes.Status500InternalServerError;

    /// <summary>Code for problems the framework produces itself (model validation, unknown routes, auth challenges).</summary>
    public static string DefaultCodeFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.Validation,
        StatusCodes.Status401Unauthorized => ErrorCodes.AuthUnauthenticated,
        StatusCodes.Status403Forbidden => ErrorCodes.AuthForbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        >= 500 => ErrorCodes.ServerError,
        _ => "http." + status.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}
