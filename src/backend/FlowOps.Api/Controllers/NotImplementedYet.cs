using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>
/// BE-03 contract stubs. Each action keeps its final route, policy and documented responses; the body
/// is replaced when its feature task lands. 501 is deliberately not documented in the contract.
/// </summary>
internal static class NotImplementedYet
{
    public static ObjectResult Stub(this ControllerBase controller, string task) =>
        controller.Problem(statusCode: StatusCodes.Status501NotImplemented, detail: $"Implemented in {task}.");
}
