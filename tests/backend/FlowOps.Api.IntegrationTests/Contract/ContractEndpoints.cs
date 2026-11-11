namespace FlowOps.Api.IntegrationTests.Contract;

/// <summary>
/// Every endpoint of docs/API_CONTRACT.md §3–8 and §10, with a body that passes input validation and the
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
        new("POST", "/api/auth/login", "/api/auth/login", """{"email":"lina@flowops.local","password":"secret"}""", Anonymous: true),
        new("POST", "/api/auth/logout", "/api/auth/logout", null, Employee),
        new("GET", "/api/auth/me", "/api/auth/me", null, Employee),
        new("POST", "/api/auth/change-password", "/api/auth/change-password", """{"currentPassword":"old","newPassword":"a-long-new-password"}""", Employee),

        // §4 Requests
        // Submission implemented in BE-10 (behaviour: Requests/SubmissionTests).
        new("POST", "/api/requests/purchase", "/api/requests/purchase",
            """{"itemName":"Laptop","description":"14 inch","estimatedCost":{"amount":"1250.500","currency":"JOD"},"justification":"Broken"}""", Employee),
        new("POST", "/api/requests/leave", "/api/requests/leave",
            """{"startDate":"2026-12-01","endDate":"2026-12-03","leaveType":"Annual","reason":null}""", Employee),
        new("GET", "/api/requests", "/api/requests?scope=visible&status=InReview&type=Leave&search=FO-2026&page=1&pageSize=20", null, Employee),
        new("GET", "/api/requests/{id}", $"/api/requests/{Id}", null, Employee),
        new("GET", "/api/requests/{id}/activity", $"/api/requests/{Id}/activity", null, Employee),
        new("POST", "/api/requests/{id}/comments", $"/api/requests/{Id}/comments", """{"body":"Which model?"}""", Employee),

        // §5 Tasks: implemented in BE-12 (behaviour: Tasks/DecisionTests).
        new("GET", "/api/tasks", "/api/tasks?status=Done", null, Finance),
        new("POST", "/api/tasks/{taskId}/approve", $"/api/tasks/{Id}/approve", """{"version":"7","comment":null}""", Finance),
        new("POST", "/api/tasks/{taskId}/reject", $"/api/tasks/{Id}/reject", """{"version":"7","reason":"Over budget"}""", Finance),
        new("POST", "/api/tasks/{taskId}/fulfill", $"/api/tasks/{Id}/fulfill", """{"version":"7","note":"PO-77"}""", "ProcurementOfficer"),

        // §6 Notifications: implemented in BE-13 (behaviour: Notifications/NotificationsApiTests).
        new("GET", "/api/notifications", "/api/notifications?unreadOnly=true", null, Employee),
        new("GET", "/api/notifications/unread-count", "/api/notifications/unread-count", null, Employee),
        new("POST", "/api/notifications/{id}/read", $"/api/notifications/{Id}/read", null, Employee),
        new("POST", "/api/notifications/read-all", "/api/notifications/read-all", null, Employee),

        // §7 Dashboard, and §8 stalled tasks below: implemented in BE-15 (behaviour: Dashboard/DashboardTests).
        new("GET", "/api/dashboard", "/api/dashboard", null, Employee),

        // §8 Administration: departments and users implemented in BE-08 (behaviour: Organization/*Tests).
        new("GET", "/api/departments", "/api/departments?includeInactive=false", null, Employee),
        new("POST", "/api/departments", "/api/departments", """{"name":"Sales","code":"SALES"}""", Admin),
        new("PUT", "/api/departments/{id}", $"/api/departments/{Id}", """{"name":"Sales","code":"SALES","isActive":false}""", Admin),
        new("GET", "/api/users", $"/api/users?search=lina&departmentId={Id}&role=Employee&includeInactive=true", null, Admin),
        new("GET", "/api/users/{id}", $"/api/users/{Id}", null, Admin),
        new("POST", "/api/users", "/api/users",
            """{"email":"new@flowops.local","displayName":"New Person","initialPassword":"a-long-initial-pw","departmentId":null,"managerId":null,"roles":["Employee"]}""", Admin),
        new("PUT", "/api/users/{id}", $"/api/users/{Id}", """{"displayName":"Renamed","departmentId":null,"managerId":null,"isActive":true}""", Admin),
        new("PUT", "/api/users/{id}/roles", $"/api/users/{Id}/roles", """{"roles":["Employee","HrOfficer"]}""", Admin),
        new("POST", "/api/users/{id}/reset-password", $"/api/users/{Id}/reset-password", """{"newPassword":"a-long-reset-pw"}""", Admin),
        // ADR-0012 role changes (BE-08d, behaviour: Organization/RoleChangeTests).
        new("GET", "/api/admin/role-changes", "/api/admin/role-changes?status=Pending&page=1&pageSize=20", null, Admin),
        new("GET", "/api/admin/role-changes/{id}", $"/api/admin/role-changes/{Id}", null, Admin),
        new("POST", "/api/admin/role-changes/{id}/approve", $"/api/admin/role-changes/{Id}/approve", """{"version":"7"}""", Admin),
        new("POST", "/api/admin/role-changes/{id}/reject", $"/api/admin/role-changes/{Id}/reject", """{"version":"7","reason":"Not now"}""", Admin),
        new("POST", "/api/admin/role-changes/{id}/cancel", $"/api/admin/role-changes/{Id}/cancel", """{"version":"7"}""", Admin),
        new("GET", "/api/admin/stalled-tasks", "/api/admin/stalled-tasks?page=2&pageSize=50", null, Admin),

        // §10 Workflows (ADR-0014): contract skeleton from P2-DOC-01, 501 until implemented.
        new("POST", "/api/requests/route-preview", "/api/requests/route-preview",
            """{"type":"EquipmentPurchase","facts":{"amount":{"amount":"1500.000","currency":"JOD"}}}""", Employee) { Stub = "P2-BE-05" },
        new("GET", "/api/admin/workflows", "/api/admin/workflows", null, Admin) { Stub = "P2-BE-07" },
        new("GET", "/api/admin/workflows/catalogue", "/api/admin/workflows/catalogue", null, Admin) { Stub = "P2-BE-07" },
        new("GET", "/api/admin/workflows/{workflowId}/versions", $"/api/admin/workflows/{Id}/versions", null, Admin) { Stub = "P2-BE-07" },
        new("GET", "/api/admin/workflows/{workflowId}/versions/{versionId}", $"/api/admin/workflows/{Id}/versions/{Id}", null, Admin) { Stub = "P2-BE-07" },
        new("POST", "/api/admin/workflows/{workflowId}/draft", $"/api/admin/workflows/{Id}/draft", null, Admin) { Stub = "P2-BE-07" },
        new("PUT", "/api/admin/workflows/{workflowId}/versions/{versionId}", $"/api/admin/workflows/{Id}/versions/{Id}",
            """{"version":"7","steps":[{"key":"manager-approval","name":"Manager approval","kind":"Approval","assignee":{"kind":"RequesterManager","role":null,"departmentId":null,"userId":null},"condition":null,"canReadRestrictedDetails":false},{"key":"finance-approval","name":"Finance approval","kind":"Approval","assignee":{"kind":"Role","role":"FinanceOfficer","departmentId":null,"userId":null},"condition":{"fact":"amount","operator":"GreaterThan","values":["1000.000"]},"canReadRestrictedDetails":false}]}""",
            Admin) { Stub = "P2-BE-07" },
        new("DELETE", "/api/admin/workflows/{workflowId}/versions/{versionId}", $"/api/admin/workflows/{Id}/versions/{Id}?version=7", null, Admin) { Stub = "P2-BE-07" },
        new("POST", "/api/admin/workflows/{workflowId}/versions/{versionId}/validate", $"/api/admin/workflows/{Id}/versions/{Id}/validate", null, Admin) { Stub = "P2-BE-07" },
        new("POST", "/api/admin/workflows/{workflowId}/versions/{versionId}/preview", $"/api/admin/workflows/{Id}/versions/{Id}/preview",
            """{"facts":{"leaveType":"Sick","days":3}}""", Admin) { Stub = "P2-BE-07" },
        new("POST", "/api/admin/workflows/{workflowId}/versions/{versionId}/publish", $"/api/admin/workflows/{Id}/versions/{Id}/publish",
            """{"version":"7"}""", Admin) { Stub = "P2-BE-07" },
    ];
}

internal sealed record ContractEndpoint(string Method, string Template, string Url, string? Body, params string[] Roles)
{
    public bool Anonymous { get; init; }

    /// <summary>The task that implements a contract-skeleton endpoint, which answers 501 until then.</summary>
    public string? Stub { get; init; }

    public ContractEndpoint(string method, string template, string url, string? body, bool Anonymous)
        : this(method, template, url, body) => this.Anonymous = Anonymous;

    public override string ToString() => $"{Method} {Template}";
}
