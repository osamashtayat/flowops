namespace FlowOps.Api.IntegrationTests.Contract;

/// <summary>
/// Every endpoint of docs/API_CONTRACT.md §3–8, with a body that passes input validation and the
/// roles that satisfy its policy. Kept by hand on purpose: it is the check that code matches the design doc.
/// </summary>
internal static class ContractEndpoints
{
    private const string Id = "01900000-0000-7000-8000-000000000001";
    private const string Employee = "Employee";
    private const string Admin = "Administrator";
    private const string Finance = "FinanceOfficer";

    public static readonly ContractEndpoint[] All =
    [
        // §3 Auth
        // §3 is implemented (BE-05) and needs a database: behaviour is covered by Auth/AuthTests.
        new("POST", "/api/auth/login", "/api/auth/login", """{"email":"lina@flowops.local","password":"secret"}""", Anonymous: true) { Implemented = true },
        new("POST", "/api/auth/logout", "/api/auth/logout", null, Employee) { Implemented = true },
        new("GET", "/api/auth/me", "/api/auth/me", null, Employee) { Implemented = true },
        new("POST", "/api/auth/change-password", "/api/auth/change-password", """{"currentPassword":"old","newPassword":"a-long-new-password"}""", Employee) { Implemented = true },

        // §4 Requests
        // Submission implemented in BE-10 (behaviour: Requests/SubmissionTests).
        new("POST", "/api/requests/purchase", "/api/requests/purchase",
            """{"itemName":"Laptop","description":"14 inch","estimatedCost":{"amount":"1250.500","currency":"JOD"},"justification":"Broken"}""", Employee) { Implemented = true },
        new("POST", "/api/requests/leave", "/api/requests/leave",
            """{"startDate":"2026-12-01","endDate":"2026-12-03","leaveType":"Annual","reason":null}""", Employee) { Implemented = true },
        new("GET", "/api/requests", "/api/requests?scope=visible&status=InReview&type=Leave&search=FO-2026&page=1&pageSize=20", null, Employee) { Implemented = true },
        new("GET", "/api/requests/{id}", $"/api/requests/{Id}", null, Employee) { Implemented = true },
        new("GET", "/api/requests/{id}/activity", $"/api/requests/{Id}/activity", null, Employee) { Implemented = true },
        new("POST", "/api/requests/{id}/comments", $"/api/requests/{Id}/comments", """{"body":"Which model?"}""", Employee) { Implemented = true },

        // §5 Tasks: implemented in BE-12 (behaviour: Tasks/DecisionTests).
        new("GET", "/api/tasks", "/api/tasks?status=Done", null, Finance) { Implemented = true },
        new("POST", "/api/tasks/{taskId}/approve", $"/api/tasks/{Id}/approve", """{"version":"7","comment":null}""", Finance) { Implemented = true },
        new("POST", "/api/tasks/{taskId}/reject", $"/api/tasks/{Id}/reject", """{"version":"7","reason":"Over budget"}""", Finance) { Implemented = true },
        new("POST", "/api/tasks/{taskId}/fulfill", $"/api/tasks/{Id}/fulfill", """{"version":"7","note":"PO-77"}""", "ProcurementOfficer") { Implemented = true },

        // §6 Notifications
        new("GET", "/api/notifications", "/api/notifications?unreadOnly=true", null, Employee),
        new("GET", "/api/notifications/unread-count", "/api/notifications/unread-count", null, Employee),
        new("POST", "/api/notifications/{id}/read", $"/api/notifications/{Id}/read", null, Employee),
        new("POST", "/api/notifications/read-all", "/api/notifications/read-all", null, Employee),

        // §7 Dashboard
        new("GET", "/api/dashboard", "/api/dashboard", null, Employee),

        // §8 Administration: departments and users implemented in BE-08 (behaviour: Organization/*Tests).
        new("GET", "/api/departments", "/api/departments?includeInactive=false", null, Employee) { Implemented = true },
        new("POST", "/api/departments", "/api/departments", """{"name":"Sales","code":"SALES"}""", Admin) { Implemented = true },
        new("PUT", "/api/departments/{id}", $"/api/departments/{Id}", """{"name":"Sales","code":"SALES","isActive":false}""", Admin) { Implemented = true },
        new("GET", "/api/users", $"/api/users?search=lina&departmentId={Id}&role=Employee&includeInactive=true", null, Admin) { Implemented = true },
        new("GET", "/api/users/{id}", $"/api/users/{Id}", null, Admin) { Implemented = true },
        new("POST", "/api/users", "/api/users",
            """{"email":"new@flowops.local","displayName":"New Person","initialPassword":"a-long-initial-pw","departmentId":null,"managerId":null,"roles":["Employee"]}""", Admin) { Implemented = true },
        new("PUT", "/api/users/{id}", $"/api/users/{Id}", """{"displayName":"Renamed","departmentId":null,"managerId":null,"isActive":true}""", Admin) { Implemented = true },
        new("PUT", "/api/users/{id}/roles", $"/api/users/{Id}/roles", """{"roles":["Employee","HrOfficer"]}""", Admin) { Implemented = true },
        new("POST", "/api/users/{id}/reset-password", $"/api/users/{Id}/reset-password", """{"newPassword":"a-long-reset-pw"}""", Admin) { Implemented = true },
        new("GET", "/api/admin/stalled-tasks", "/api/admin/stalled-tasks?page=2&pageSize=50", null, Admin),
    ];
}

internal sealed record ContractEndpoint(string Method, string Template, string Url, string? Body, params string[] Roles)
{
    public bool Anonymous { get; init; }

    /// <summary>Its feature task has landed: it no longer answers 501 and may need a database.</summary>
    public bool Implemented { get; init; }

    public ContractEndpoint(string method, string template, string url, string? body, bool Anonymous)
        : this(method, template, url, body) => this.Anonymous = Anonymous;

    public override string ToString() => $"{Method} {Template}";
}
