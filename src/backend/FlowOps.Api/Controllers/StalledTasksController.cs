using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §8 stalled tasks: metadata only, admin only. Implemented in BE-15.</summary>
[ApiController]
[Route("api/admin/stalled-tasks")]
[Tags("Administration")]
[Authorize(Policy = Policies.Admin)]
public sealed class StalledTasksController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<StalledTaskDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult<PagedResult<StalledTaskDto>> List([FromQuery] PageRequest query) => this.Stub("BE-15");
}
