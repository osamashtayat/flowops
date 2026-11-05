using FlowOps.Api.Auth;
using FlowOps.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §8 departments. Listing is open to any signed-in user; changes are admin-only.</summary>
[ApiController]
[Route("api/departments")]
[Tags("Administration")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class DepartmentsController(DepartmentService departments) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List([FromQuery] DepartmentListQuery query, CancellationToken ct) =>
        Ok(await departments.ListAsync(query, ct));

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest body, CancellationToken ct)
    {
        var created = await departments.CreateAsync(body, ct);
        // No GET-by-id exists for departments (API_CONTRACT.md §8), so Location points at the list.
        return Created(Url.Action(nameof(List)), created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpdateDepartmentRequest body, CancellationToken ct) =>
        Ok(await departments.UpdateAsync(id, body, ct));
}
