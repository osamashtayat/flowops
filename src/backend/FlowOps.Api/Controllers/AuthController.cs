using FlowOps.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>API_CONTRACT.md §3. Implemented in BE-05.</summary>
[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController : ControllerBase
{
    private const string Task = "BE-05";

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status423Locked)]
    public ActionResult<CurrentUserDto> Login(LoginRequest body) => this.Stub(Task);

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout() => this.Stub(Task);

    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserDto> Me() => this.Stub(Task);

    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult ChangePassword(ChangePasswordRequest body) => this.Stub(Task);
}
