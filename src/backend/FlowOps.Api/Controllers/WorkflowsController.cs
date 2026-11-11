using FlowOps.Api.Auth;
using FlowOps.Application.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlowOps.Api.Controllers;

/// <summary>
/// API_CONTRACT.md §10, workflow administration (ADR-0014). Contract skeleton from P2-DOC-01: every action answers 501
/// until P2-BE-07 implements it. Policy <see cref="Policies.ManageWorkflows"/>; writes re-check the caller in the database.
/// </summary>
[ApiController]
[Route("api/admin/workflows")]
[Tags("Workflows")]
[Authorize(Policy = Policies.ManageWorkflows)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class WorkflowsController : ControllerBase
{
    private const string Task = "P2-BE-07";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WorkflowSummaryDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<WorkflowSummaryDto>> List() => this.Stub(Task);

    [HttpGet("catalogue")]
    [ProducesResponseType<WorkflowCatalogueDto>(StatusCodes.Status200OK)]
    public ActionResult<WorkflowCatalogueDto> Catalogue() => this.Stub(Task);

    [HttpGet("{workflowId:guid}/versions")]
    [ProducesResponseType<IReadOnlyList<WorkflowVersionSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<IReadOnlyList<WorkflowVersionSummaryDto>> Versions(Guid workflowId) => this.Stub(Task);

    [HttpGet("{workflowId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType<WorkflowVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<WorkflowVersionDto> GetVersion(Guid workflowId, Guid versionId) => this.Stub(Task);

    /// <summary>Starts a Draft as a copy of the Published version. One Draft per workflow at a time.</summary>
    [HttpPost("{workflowId:guid}/draft")]
    [ProducesResponseType<WorkflowVersionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<WorkflowVersionDto> CreateDraft(Guid workflowId) => this.Stub(Task);

    [HttpPut("{workflowId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType<WorkflowVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public ActionResult<WorkflowVersionDto> UpdateDraft(Guid workflowId, Guid versionId, UpdateWorkflowDraftRequest body) =>
        this.Stub(Task);

    /// <summary>Discards a Draft. <paramref name="version"/> is the draft's last-seen token.</summary>
    [HttpDelete("{workflowId:guid}/versions/{versionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public IActionResult DiscardDraft(Guid workflowId, Guid versionId, [FromQuery] string version) => this.Stub(Task);

    [HttpPost("{workflowId:guid}/versions/{versionId:guid}/validate")]
    [ProducesResponseType<WorkflowValidationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<WorkflowValidationDto> Validate(Guid workflowId, Guid versionId) => this.Stub(Task);

    [HttpPost("{workflowId:guid}/versions/{versionId:guid}/preview")]
    [ProducesResponseType<RoutePreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public ActionResult<RoutePreviewDto> Preview(Guid workflowId, Guid versionId, WorkflowPreviewRequest body) => this.Stub(Task);

    /// <summary>Publishes a Draft (single administrator, ADR-0014 Q1); retires the previous Published version.</summary>
    [HttpPost("{workflowId:guid}/versions/{versionId:guid}/publish")]
    [ProducesResponseType<WorkflowVersionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public ActionResult<WorkflowVersionDto> Publish(Guid workflowId, Guid versionId, PublishWorkflowRequest body) => this.Stub(Task);
}
