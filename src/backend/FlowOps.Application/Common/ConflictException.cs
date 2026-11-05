namespace FlowOps.Application.Common;

/// <summary>A state conflict such as a stale version or a duplicate (409).</summary>
public sealed class ConflictException(string code, string message) : AppException(code, message);
