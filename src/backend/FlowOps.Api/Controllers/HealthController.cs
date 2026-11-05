using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>Liveness probe. Anonymous by design: it reveals nothing beyond "the process answers".</summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthDto>(StatusCodes.Status200OK)]
    public ActionResult<HealthDto> Get() => Ok(HealthDto.Ok);
}

public sealed record HealthDto(string Status)
{
    public static readonly HealthDto Ok = new("ok");
}
