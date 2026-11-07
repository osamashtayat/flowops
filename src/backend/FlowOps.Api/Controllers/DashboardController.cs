using FlowOps.Application.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §7: one role-aware summary for the signed-in user.</summary>
[ApiController]
[Route("api/dashboard")]
[Tags("Dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));
}
