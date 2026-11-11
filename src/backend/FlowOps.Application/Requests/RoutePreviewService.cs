using FlowOps.Application.Workflows;
using FlowOps.Domain.Errors;
using FlowOps.Domain.Workflows;

namespace FlowOps.Application.Requests;

/// <summary>
/// <c>POST /api/requests/route-preview</c> (API_CONTRACT.md §4, ADR-0014 Q5): the route a request with these facts would
/// take under its type's Published workflow. Reads only the facts the catalogue gives that type. Never saves.
/// </summary>
public sealed class RoutePreviewService(IProcessDefinitionSource workflows)
{
    public async Task<RoutePreviewDto> PreviewAsync(RoutePreviewRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var type = body.Type!.Value;
        var facts = FactsOf(FactCatalogue.For(type), body.Facts!);
        var published = await workflows.PublishedAsync(type, ct);
        return new RoutePreviewDto(RequestRoute.Preview(published, facts));
    }

    private static RequestFacts FactsOf(IReadOnlyList<FactKey> wanted, RouteFactsInput input) => new(
        wanted.Contains(FactKey.Amount) ? Required(input.Amount, "amount").ToMoney("facts.amount") : null,
        wanted.Contains(FactKey.LeaveType) ? Required(input.LeaveType, "leaveType") : null,
        wanted.Contains(FactKey.Days) ? Required(input.Days, "days") : null);

    private static T Required<T>(T? value, string fact)
        where T : class =>
        value ?? throw DomainErrors.Validation($"{fact} is required for this request type.", $"facts.{fact}");

    private static T Required<T>(T? value, string fact)
        where T : struct =>
        value ?? throw DomainErrors.Validation($"{fact} is required for this request type.", $"facts.{fact}");
}
