using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Api.Requests;
using FlowOps.Application.Requests;
using FlowOps.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    /// <summary>The route a request with these facts would take (ADR-0014 Q5). Contract skeleton: 501 until P2-BE-05.</summary>
    [HttpPost("route-preview")]
    [Authorize(Policy = Policies.CanSubmit)]
    [ProducesResponseType<RoutePreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public ActionResult<RoutePreviewDto> RoutePreview(RoutePreviewRequest body) => this.Stub("P2-BE-05");

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
    [EnableRateLimiting(CommentRateLimitPolicy.Name)]
    [ProducesResponseType<ActivityDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ActivityDto>> AddComment(Guid id, AddCommentRequest body, CancellationToken ct) =>
        CreatedAtAction(nameof(Activity), new { id }, await comments.AddAsync(id, body, ct));

    private CreatedAtActionResult Submitted(RequestDetailsDto created) => CreatedAtAction(nameof(Get), new { id = created.Id }, created);
}
