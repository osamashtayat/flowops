using FlowOps.Domain.Organization;
using FlowOps.Domain.Requests;

namespace FlowOps.Infrastructure.Seeding;

/// <summary>
/// The demo company created by <c>--seed-demo</c> (BE-16): plain data, so the people and stories are readable in one place.
/// Every account shares <see cref="Password"/>, a development-only test value documented in the README; it is not a secret.
/// </summary>
internal static class DemoCompany
{
    public const string Password = "FlowOps-Demo-2026!";

    /// <summary>Its presence means the demo company was already seeded.</summary>
    public const string MarkerEmail = "alice.admin@demo.flowops.test";

    public static readonly DemoDepartment[] Departments =
    [
        new("ENG", "Engineering"),
        new("FIN", "Finance"),
        new("OPS", "Operations"),
    ];

    /// <summary>
    /// Rami left the company <see cref="ManagerChangeDaysAgo"/> days ago (inactive) and Nour now reports to Dana. Nour's
    /// purchase from before that still waits for Rami: a stalled task (<c>AssigneeInactive</c>). Farah's own purchase at
    /// finance is the other kind (<c>NoEligibleRoleMember</c>: she is the only finance officer).
    /// </summary>
    public static readonly DemoUser[] Users =
    [
        new("alice.admin", "Alice Haddad", null, null, [Roles.Administrator]),
        new("omar.admin", "Omar Khalil", null, null, [Roles.Administrator]),
        new("dana.director", "Dana Nasser", "OPS", null, [Roles.Employee]),
        new("samir.manager", "Samir Odeh", "ENG", "dana.director", [Roles.Employee]),
        new("lina.dev", "Lina Saleh", "ENG", "samir.manager", [Roles.Employee]),
        new("karim.dev", "Karim Mansour", "ENG", "samir.manager", [Roles.Employee]),
        new("farah.finance", "Farah Jaber", "FIN", "dana.director", [Roles.Employee, Roles.FinanceOfficer]),
        new("hana.hr", "Hana Qasem", "OPS", "dana.director", [Roles.Employee, Roles.HrOfficer]),
        new("yousef.procurement", "Yousef Azar", "OPS", "dana.director", [Roles.Employee, Roles.ProcurementOfficer]),
        new("rami.former", "Rami Hamdan", "OPS", "dana.director", [Roles.Employee], IsActive: false),
        new("nour.ops", "Nour Barakat", "OPS", "dana.director", [Roles.Employee], PreviousManagerKey: "rami.former"),
    ];

    /// <summary>Requests older than this went to the requester's previous manager.</summary>
    public const double ManagerChangeDaysAgo = 10;

    /// <summary>Omar asks to add Employee to his own roles; Alice has to approve it (ADR-0012).</summary>
    public static readonly DemoRoleChange PendingRoleChange = new("omar.admin", [Roles.Administrator, Roles.Employee], DaysAgo: 1);

    private static DemoDecision Approve(string by, double daysAgo, string? comment = null) => new(by, TaskDecisionAction.Approve, comment, daysAgo);

    private static DemoDecision Reject(string by, double daysAgo, string reason) => new(by, TaskDecisionAction.Reject, reason, daysAgo);

    private static DemoDecision Fulfill(string by, double daysAgo, string? note = null) => new(by, TaskDecisionAction.Fulfill, note, daysAgo);

    public static readonly DemoRequest[] Requests =
    [
        // Purchases: one per state, plus both kinds of stalled task.
        new("lina.dev", 1, new DemoPurchase("Laptop", "14-inch developer laptop, 32 GB RAM", 1150m, "Current laptop is five years old."), []),
        new("karim.dev", 3, new DemoPurchase("27-inch monitor", "4K monitor with USB-C", 320.5m, "Second screen for code reviews."),
            [Approve("samir.manager", 2, "Fine by me.")], Comment: ("karim.dev", "Any USB-C model with a stand is fine.")),
        new("samir.manager", 6, new DemoPurchase("Test devices", "Two mid-range Android phones", 640m, "Mobile QA for the customer app."),
            [Approve("dana.director", 5), Approve("farah.finance", 4, "Within the Q4 budget.")]),
        new("lina.dev", 10, new DemoPurchase("Noise-cancelling headset", "Wireless headset with microphone", 145.75m, "Open-plan office calls."),
            [Approve("samir.manager", 9), Approve("farah.finance", 8), Fulfill("yousef.procurement", 6, "Delivered to the Engineering desk.")]),
        new("karim.dev", 8, new DemoPurchase("Gaming chair", "Racing-style chair with RGB lights", 410m, "More comfortable for long days."),
            [Reject("samir.manager", 7, "Please choose a model from the approved ergonomic list.")]),
        new("lina.dev", 7, new DemoPurchase("Meeting-room display", "65-inch display with wall mount", 1890m, "Engineering stand-ups."),
            [Approve("samir.manager", 6), Reject("farah.finance", 5, "Facilities already budgeted displays for all rooms next quarter.")]),
        new("nour.ops", 12, new DemoPurchase("Label printer", "Thermal label printer for the warehouse", 230m, "Shipping labels are printed by hand."), []),
        new("farah.finance", 4, new DemoPurchase("Financial calculator", "Programmable financial calculator", 95m, "Loan and depreciation schedules."),
            [Approve("dana.director", 3)]),

        // Leave: one per state. Dates are counted from the seed day, so they are always in the future.
        new("karim.dev", 1, new DemoLeave(LeaveType.Annual, StartsInDays: 14, Days: 5, "Family trip."), []),
        new("lina.dev", 2, new DemoLeave(LeaveType.Annual, StartsInDays: 21, Days: 3, null), [Approve("samir.manager", 1)]),
        new("samir.manager", 9, new DemoLeave(LeaveType.Annual, StartsInDays: 10, Days: 2, "Moving house."),
            [Approve("dana.director", 8), Approve("hana.hr", 7, "Balance checked.")]),
        new("yousef.procurement", 5, new DemoLeave(LeaveType.Unpaid, StartsInDays: 7, Days: 10, "Personal matters."),
            [Reject("dana.director", 4, "Quarter-end stock count needs you that week; please pick other dates.")]),
        new("nour.ops", 13, new DemoLeave(LeaveType.Other, StartsInDays: 30, Days: 1, "Graduation ceremony."),
            [Approve("rami.former", 12), Reject("hana.hr", 11, "Please resubmit as Annual leave; Other needs a document.")]),
    ];
}

internal sealed record DemoDepartment(string Code, string Name);

internal sealed record DemoUser(
    string Key, string DisplayName, string? DepartmentCode, string? ManagerKey, string[] Roles,
    bool IsActive = true, string? PreviousManagerKey = null)
{
    public string Email => $"{Key}@demo.flowops.test";
}

internal sealed record DemoPurchase(string ItemName, string Description, decimal AmountJod, string Justification);

internal sealed record DemoLeave(LeaveType Type, int StartsInDays, int Days, string? Reason);

internal sealed record DemoDecision(string By, TaskDecisionAction Action, string? Comment, double DaysAgo);

/// <summary>Exactly one of <see cref="Purchase"/> or <see cref="Leave"/>; decisions in order, each later than the one before.</summary>
internal sealed record DemoRequest(
    string Requester, double DaysAgo, object Details, DemoDecision[] Decisions, (string By, string Body)? Comment = null)
{
    public DemoPurchase? Purchase => Details as DemoPurchase;

    public DemoLeave? Leave => Details as DemoLeave;
}

internal sealed record DemoRoleChange(string UserKey, string[] ProposedRoles, double DaysAgo);
