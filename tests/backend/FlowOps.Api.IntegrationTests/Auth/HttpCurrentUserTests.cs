using System.Security.Claims;
using FlowOps.Api.Auth;
using FlowOps.Domain.Organization;
using Microsoft.AspNetCore.Http;

namespace FlowOps.Api.IntegrationTests.Auth;

public sealed class HttpCurrentUserTests
{
    private static readonly Guid UserId = new("01900000-0000-7000-8000-0000000000aa");

    [Fact]
    public void AuthenticatedPrincipal_ExposesIdAndRoles()
    {
        var user = For(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, UserId.ToString()), new Claim(ClaimTypes.Role, Roles.FinanceOfficer)],
            authenticationType: "test"));

        user.IsAuthenticated.ShouldBeTrue();
        user.Id.ShouldBe(UserId);
        user.Roles.ShouldBe([Roles.FinanceOfficer]);
        user.IsInRole(Roles.FinanceOfficer).ShouldBeTrue();
        user.IsInRole(Roles.Administrator).ShouldBeFalse();
    }

    [Fact]
    public void Anonymous_HasNoIdAndNoRoles()
    {
        var user = For(new ClaimsIdentity());

        user.IsAuthenticated.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => user.Id);
        user.Roles.ShouldBeEmpty();
        user.IsInRole(Roles.Employee).ShouldBeFalse();
    }

    [Fact]
    public void AuthenticatedWithoutValidId_Throws() =>
        Should.Throw<InvalidOperationException>(() =>
            For(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], authenticationType: "test")).Id);

    private static HttpCurrentUser For(ClaimsIdentity identity) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } });
}
