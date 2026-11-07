using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Organization;

public sealed class RoleChangeTests
{
    private static readonly Guid Ada = Guid.CreateVersion7();
    private static readonly Guid Bea = Guid.CreateVersion7();

    private static RoleChange Proposal() =>
        RoleChange.Propose(Ada, [Roles.Administrator], [Roles.FinanceOfficer, Roles.Administrator, Roles.Administrator], T0);

    [Fact]
    public void Propose_IsPendingFor72HoursWithSortedDistinctRoles()
    {
        var change = Proposal();

        change.Status.ShouldBe(RoleChangeStatus.Pending);
        change.UserId.ShouldBe(Ada);
        change.ProposedRoles.ShouldBe([Roles.Administrator, Roles.FinanceOfficer]);
        change.PreviousRoles.ShouldBe([Roles.Administrator]);
        change.ExpiresAt.ShouldBe(T0.AddHours(72));
        change.DecidedAt.ShouldBeNull();
    }

    [Fact]
    public void Propose_SameRoles_ThrowsValidation() =>
        ShouldFailWith(() => RoleChange.Propose(Ada, [Roles.Administrator], [Roles.Administrator], T0), ErrorCodes.Validation);

    [Fact]
    public void Propose_UnknownRole_ThrowsValidation() =>
        ShouldFailWith(() => RoleChange.Propose(Ada, [Roles.Administrator], ["Manager"], T0), ErrorCodes.Validation);

    [Fact]
    public void Approve_ByAnotherAdmin_ClosesIt()
    {
        var change = Proposal();

        change.Approve(Bea, T0.AddHours(1));

        change.Status.ShouldBe(RoleChangeStatus.Approved);
        change.DecidedById.ShouldBe(Bea);
        change.DecidedAt.ShouldBe(T0.AddHours(1));
    }

    [Fact]
    public void Approve_ByTheProposer_ThrowsSelfApproval()
    {
        var change = Proposal();

        ShouldFailWith(() => change.Approve(Ada, T0), ErrorCodes.RoleChangeSelfApproval);
        change.Status.ShouldBe(RoleChangeStatus.Pending);
    }

    [Fact]
    public void Reject_ByTheProposer_ThrowsSelfApproval() =>
        ShouldFailWith(() => Proposal().Reject(Ada, "no", T0), ErrorCodes.RoleChangeSelfApproval);

    [Fact]
    public void Reject_StoresTheTrimmedReason()
    {
        var change = Proposal();

        change.Reject(Bea, "  Not now  ", T0);

        change.Status.ShouldBe(RoleChangeStatus.Rejected);
        change.RejectionReason.ShouldBe("Not now");
    }

    [Fact]
    public void Reject_BlankReason_ThrowsValidation() =>
        ShouldFailWith(() => Proposal().Reject(Bea, "  ", T0), ErrorCodes.Validation);

    [Fact]
    public void Decide_AfterADecision_ThrowsNotPending()
    {
        var change = Proposal();
        change.Approve(Bea, T0);

        ShouldFailWith(() => change.Reject(Bea, "late", T0), ErrorCodes.RoleChangeNotPending);
    }

    [Fact]
    public void Decide_AtExpiry_ThrowsNotPending()
    {
        var change = Proposal();

        ShouldFailWith(() => change.Approve(Bea, T0 + RoleChange.Lifetime), ErrorCodes.RoleChangeNotPending);
        change.StatusAt(T0 + RoleChange.Lifetime).ShouldBe(RoleChangeStatus.Expired);
        change.StatusAt(T0 + RoleChange.Lifetime - TimeSpan.FromSeconds(1)).ShouldBe(RoleChangeStatus.Pending);
    }

    [Fact]
    public void Cancel_ByTheProposer_ClosesIt()
    {
        var change = Proposal();

        change.Cancel(Ada, T0);

        change.Status.ShouldBe(RoleChangeStatus.Cancelled);
        change.DecidedById.ShouldBe(Ada);
    }

    [Fact]
    public void Cancel_BySomeoneElse_ThrowsNotProposer() =>
        ShouldFailWith(() => Proposal().Cancel(Bea, T0), ErrorCodes.RoleChangeNotProposer);

    [Fact]
    public void ExpireIfDue_StoresExpiryOnlyOncePastTheDeadline()
    {
        var change = Proposal();

        change.ExpireIfDue(T0.AddHours(71)).ShouldBeFalse();
        change.Status.ShouldBe(RoleChangeStatus.Pending);

        change.ExpireIfDue(T0.AddHours(80)).ShouldBeTrue();
        change.Status.ShouldBe(RoleChangeStatus.Expired);
        change.DecidedAt.ShouldBe(change.ExpiresAt);
        change.ExpireIfDue(T0.AddHours(90)).ShouldBeFalse();
    }

    [Theory]
    [InlineData(RoleChangeStatus.Pending, true, false)]
    [InlineData(RoleChangeStatus.Approved, false, false)]
    [InlineData(RoleChangeStatus.Expired, false, false)]
    public void Actions_DependOnStatusAndWhoAsks(RoleChangeStatus status, bool otherAdminDecides, bool otherAdminCancels)
    {
        RoleChange.CanDecide(status, Ada, Bea).ShouldBe(otherAdminDecides);
        RoleChange.CanCancel(status, Ada, Bea).ShouldBe(otherAdminCancels);
        RoleChange.CanDecide(status, Ada, Ada).ShouldBeFalse();
        RoleChange.CanCancel(status, Ada, Ada).ShouldBe(status == RoleChangeStatus.Pending);
    }
}
