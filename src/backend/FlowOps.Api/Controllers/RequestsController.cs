using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §4. Submission lands in BE-10, queries and comments in BE-11.</summary>
[ApiController]
[Route("api/requests")]
[Tags("Requests")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class RequestsController : ControllerBase
{
    [HttpPost("purchase")]
    [Authorize(Policy = Policies.CanSubmit)]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public ActionResult<RequestDetailsDto> SubmitPurchase(SubmitPurchaseRequest body) => this.Stub("BE-10");

    [HttpPost("leave")]
    [Authorize(Policy = Policies.CanSubmit)]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public ActionResult<RequestDetailsDto> SubmitLeave(SubmitLeaveRequest body) => this.Stub("BE-10");

    [HttpGet]
    [ProducesResponseType<PagedResult<RequestSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<PagedResult<RequestSummaryDto>> List([FromQuery] RequestListQuery query) => this.Stub("BE-11");

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<RequestDetailsDto> Get(Guid id) => this.Stub("BE-11");

    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<ActivityDto>> Activity(Guid id) => this.Stub("BE-11");

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<ActivityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<ActivityDto> AddComment(Guid id, AddCommentRequest body) => this.Stub("BE-11");
}
