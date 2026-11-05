using FlowOps.Application.Common;
using FlowOps.Application.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §6: the caller's own notifications only. Implemented in BE-13.</summary>
[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
public sealed class NotificationsController : ControllerBase
{
    private const string Task = "BE-13";

    [HttpGet]
    [ProducesResponseType<PagedResult<NotificationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<PagedResult<NotificationDto>> List([FromQuery] NotificationListQuery query) => this.Stub(Task);

    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public ActionResult<UnreadCountDto> UnreadCount() => this.Stub(Task);

    /// <summary>Idempotent. Another user's notification id returns 404.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult MarkRead(Guid id) => this.Stub(Task);

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult MarkAllRead() => this.Stub(Task);
}
