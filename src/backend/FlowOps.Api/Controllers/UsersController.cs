using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §8 users. Admin only.</summary>
[ApiController]
[Route("api/users")]
[Tags("Administration")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class UsersController(UserAdministrationService users) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResult<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<UserDto>>> List([FromQuery] UserListQuery query, CancellationToken ct) =>
        Ok(await users.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Get(Guid id, CancellationToken ct) => Ok(await users.GetAsync(id, ct));

    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest body, CancellationToken ct)
    {
        var created = await users.CreateAsync(body, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest body, CancellationToken ct) =>
        Ok(await users.UpdateAsync(id, body, ct));

    /// <summary>200 with the user, or 202 with the pending change when admins edit their own roles (ADR-0012 §3).</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<RoleChangeDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateRoles(Guid id, UpdateUserRolesRequest body, CancellationToken ct) =>
        await users.UpdateRolesAsync(id, body, ct) switch
        {
            { PendingChange: { } pending } => AcceptedAtAction(nameof(RoleChangesController.Get), "RoleChanges", new { id = pending.Id }, pending),
            { User: var user } => Ok(user),
        };

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest body, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, body, ct);
        return NoContent();
    }
}
