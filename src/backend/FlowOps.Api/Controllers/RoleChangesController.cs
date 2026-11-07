using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>ADR-0012: administrators' own role changes, decided by another administrator. Admin only.</summary>
[ApiController]
[Route("api/admin/role-changes")]
[Tags("Administration")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class RoleChangesController(RoleChangeService roleChanges) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<RoleChangeDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RoleChangeDto>>> List([FromQuery] RoleChangeListQuery query, CancellationToken ct) =>
        Ok(await roleChanges.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RoleChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoleChangeDto>> Get(Guid id, CancellationToken ct) => Ok(await roleChanges.GetAsync(id, ct));

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType<RoleChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RoleChangeDto>> Approve(Guid id, RoleChangeDecisionRequest body, CancellationToken ct) =>
        Ok(await roleChanges.ApproveAsync(id, body, ct));

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType<RoleChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RoleChangeDto>> Reject(Guid id, RejectRoleChangeRequest body, CancellationToken ct) =>
        Ok(await roleChanges.RejectAsync(id, body, ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<RoleChangeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RoleChangeDto>> Cancel(Guid id, RoleChangeDecisionRequest body, CancellationToken ct) =>
        Ok(await roleChanges.CancelAsync(id, body, ct));
}
