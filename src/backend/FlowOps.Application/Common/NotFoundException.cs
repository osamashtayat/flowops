using FlowOps.Domain.Errors;

namespace FlowOps.Application.Common;

/// <summary>The resource does not exist <b>or is not visible</b> to the caller (404, never 403).</summary>
public sealed class NotFoundException(string message = "The resource was not found.")
    : AppException(ErrorCodes.NotFound, message);
