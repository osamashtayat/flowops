using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §4.</summary>
[ApiController]
[Route("api/requests")]
[Tags("Requests")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class RequestsController(
    RequestSubmissionService submission, RequestQueryService queries, RequestCommentService comments) : ControllerBase
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
    public async Task<ActionResult<PagedResult<RequestSummaryDto>>> List([FromQuery] RequestListQuery query, CancellationToken ct) =>
        Ok(await queries.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailsDto>> Get(Guid id, CancellationToken ct) => Ok(await queries.GetAsync(id, ct));

    [HttpGet("{id:guid}/activity")]
    [ProducesResponseType<IReadOnlyList<ActivityDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> Activity(Guid id, CancellationToken ct) =>
        Ok(await queries.GetActivityAsync(id, ct));

    [HttpPost("{id:guid}/comments")]
    [ProducesResponseType<ActivityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> AddComment(Guid id, AddCommentRequest body, CancellationToken ct) =>
        CreatedAtAction(nameof(Activity), new { id }, await comments.AddAsync(id, body, ct));

    private CreatedAtActionResult Submitted(RequestDetailsDto created) => CreatedAtAction(nameof(Get), new { id = created.Id }, created);
}
