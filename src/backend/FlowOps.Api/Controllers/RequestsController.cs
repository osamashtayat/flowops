using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §4. Queries and comments land in BE-11.</summary>
[ApiController]
[Route("api/requests")]
[Tags("Requests")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class RequestsController(RequestSubmissionService submission) : ControllerBase
{
    [HttpPost("purchase")]
    [Authorize(Policy = Policies.CanSubmit)]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestDetailsDto>> SubmitPurchase(SubmitPurchaseRequest body, CancellationToken ct) =>
        Submitted(await submission.SubmitPurchaseAsync(body, ct));

    [HttpPost("leave")]
    [Authorize(Policy = Policies.CanSubmit)]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestDetailsDto>> SubmitLeave(SubmitLeaveRequest body, CancellationToken ct) =>
        Submitted(await submission.SubmitLeaveAsync(body, ct));

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

    private CreatedAtActionResult Submitted(RequestDetailsDto created) => CreatedAtAction(nameof(Get), new { id = created.Id }, created);
}
