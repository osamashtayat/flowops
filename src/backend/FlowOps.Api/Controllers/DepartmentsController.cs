using FlowOps.Api.Auth;
using FlowOps.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §8 departments. Listing is open to any signed-in user; changes are admin-only. BE-08.</summary>
[ApiController]
[Route("api/departments")]
[Tags("Administration")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class DepartmentsController : ControllerBase
{
    private const string Task = "BE-08";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DepartmentDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<DepartmentDto>> List([FromQuery] DepartmentListQuery query) => this.Stub(Task);

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<DepartmentDto> Create(CreateDepartmentRequest body) => this.Stub(Task);

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<DepartmentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<DepartmentDto> Update(Guid id, UpdateDepartmentRequest body) => this.Stub(Task);
}
