using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using FlowOps.Application.Common;
using FlowOps.Domain.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.IntegrationTests.Errors;

/// <summary>Test-only endpoints that throw, so the real error pipeline can be exercised end to end.</summary>
[ApiController]
[AllowAnonymous]
[Route("test/errors")]
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "MVC actions must be instance methods.")]
public sealed class ThrowingController : ControllerBase
{
    public const string SecretInternals = "connection to db-internal-7 failed: password=hunter2";

    [HttpGet("domain/{code}")]
    public IActionResult Domain(string code) => throw new DomainException(code, "Domain detail for " + code);

    [HttpGet("not-found")]
    public IActionResult NotFoundError() => throw new NotFoundException();

    [HttpGet("forbidden")]
    public IActionResult ForbiddenError() => throw new ForbiddenException(ErrorCodes.TaskNotEligible, "Not your task.");

    [HttpGet("conflict")]
    public IActionResult ConflictError() => throw new ConflictException(ErrorCodes.ConcurrencyConflict, "Stale version.");

    [HttpGet("unhandled")]
    public IActionResult Unhandled() => throw new InvalidOperationException(SecretInternals);

    [HttpPost("validation")]
    public IActionResult Validation(SampleRequest body) => Ok(body);

    // Input DTOs use init properties, not positional parameters: MVC reports positional-record
    // parameter errors under the C# name ("ItemName") and ignores the JSON naming policy.
    public sealed record SampleRequest
    {
        [Required]
        [StringLength(5)]
        public string? ItemName { get; init; }
    }
}
