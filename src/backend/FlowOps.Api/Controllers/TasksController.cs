using FlowOps.Api.Auth;
using FlowOps.Application.Common;
using FlowOps.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §5: the inbox and the three decisions (one pipeline). Implemented in BE-12.</summary>
[ApiController]
[Route("api/tasks")]
[Tags("Tasks")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class TasksController(TaskQueryService tasks, TaskDecisionService decisions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.TasksView)]
    [ProducesResponseType<PagedResult<InboxItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<InboxItemDto>>> Inbox([FromQuery] TaskListQuery query, CancellationToken ct) =>
        Ok(await tasks.ListAsync(query, ct));

    [HttpPost("{taskId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Approve(Guid taskId, ApproveTaskRequest body, CancellationToken ct)
    {
        await decisions.ApproveAsync(taskId, body, ct);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Reject(Guid taskId, RejectTaskRequest body, CancellationToken ct)
    {
        await decisions.RejectAsync(taskId, body, ct);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/fulfill")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Fulfill(Guid taskId, FulfillTaskRequest body, CancellationToken ct)
    {
        await decisions.FulfillAsync(taskId, body, ct);
        return NoContent();
    }
}
