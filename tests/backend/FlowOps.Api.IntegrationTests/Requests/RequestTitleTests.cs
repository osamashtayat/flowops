using System.Diagnostics;
using FlowOps.Application.Requests;
using FlowOps.Domain.Common;
using FlowOps.Domain.Requests;

namespace FlowOps.Api.IntegrationTests.Requests;

/// <summary>
/// Request-type dispatch has one home and no silent fallback (#84): every known type has a label, and an unknown type
/// or impossible details throw instead of being called a purchase. No database.
/// </summary>
public sealed class RequestTitleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void TypeLabel_CoversEveryRequestType() =>
        Enum.GetValues<RequestType>().Select(RequestTitle.TypeLabel).ShouldBe(["purchase request", "leave request"]);

    [Fact]
    public void TypeLabel_ForAnUnknownType_Throws() =>
        Should.Throw<UnreachableException>(() => RequestTitle.TypeLabel((RequestType)99));

    [Fact]
    public void For_PurchaseDetails_IsTheItemName() =>
        RequestTitle.For(Laptop(), leave: null).ShouldBe("Laptop");

    [Fact]
    public void For_LeaveDetails_IsTypeAndDates() =>
        RequestTitle.For(purchase: null, Leave()).ShouldBe("Annual leave · 10–12 Nov");

    [Fact]
    public void For_NoDetails_Throws() =>
        Should.Throw<UnreachableException>(() => RequestTitle.For(purchase: null, leave: null));

    [Fact]
    public void For_BothDetails_Throws() =>
        Should.Throw<UnreachableException>(() => RequestTitle.For(Laptop(), Leave()));

    private static PurchaseDetails Laptop() =>
        PurchaseDetails.Create("Laptop", "14 inch", Money.Create(1250.500m, SupportedCurrencies.Jod), "Broken");

    private static LeaveDetails Leave() =>
        LeaveDetails.Create(new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), LeaveType.Annual, "Family", Now, TimeZoneInfo.Utc);
}
