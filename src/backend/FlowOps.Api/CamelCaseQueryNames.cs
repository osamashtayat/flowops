using System.Text.Json;
using FlowOps.Application.Common;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace FlowOps.Api;

/// <summary>
/// Query-string DTOs (Application types named <c>…Query</c>, e.g. <c>RequestListQuery</c>, and <see cref="PageRequest"/>) bind and document their
/// properties in camelCase (<c>pageSize</c>), as API_CONTRACT.md spells them. Without this, the generated contract lists
/// <c>PageSize</c>; binding itself is case-insensitive, so either spelling is accepted.
/// </summary>
public sealed class CamelCaseQueryNames : IBindingMetadataProvider
{
    public void CreateBindingMetadata(BindingMetadataProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Key is { MetadataKind: ModelMetadataKind.Property, ContainerType: { } container, Name: { } name }
            && (container.Name.EndsWith("Query", StringComparison.Ordinal) || container.IsAssignableTo(typeof(PageRequest)))
            && context.BindingMetadata.BinderModelName is null)
        {
            context.BindingMetadata.BinderModelName = JsonNamingPolicy.CamelCase.ConvertName(name);
        }
    }
}
