using FlowOps.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §7. Implemented in BE-15.</summary>
[ApiController]
[Route("api/dashboard")]
[Tags("Dashboard")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<DashboardDto> Get() => this.Stub("BE-15");
}
