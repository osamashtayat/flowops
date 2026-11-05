using Microsoft.Extensions.Options;

namespace FlowOps.Application.Common;

/// <summary><c>FlowOps:CompanyTimeZone</c> (DOMAIN_MODEL.md §2.3), validated at start-up by the Api.</summary>
public sealed class CompanyOptions
{
    public const string SectionName = "FlowOps";
    public const string DefaultTimeZone = "Asia/Amman";

    public string CompanyTimeZone { get; set; } = DefaultTimeZone;
}

/// <summary>The company's time zone: "today" for leave dates and the year in request numbers are company dates, not UTC.</summary>
public sealed class CompanyCalendar(IOptions<CompanyOptions> options)
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.CompanyTimeZone);

    public int YearOf(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, TimeZone).Year;
}
