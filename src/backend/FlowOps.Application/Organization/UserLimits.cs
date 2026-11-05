namespace FlowOps.Application.Organization;

/// <summary>User input limits (DOMAIN_MODEL.md §2.2, AUTHORIZATION.md §1.6). Identity options reuse these in BE-05.</summary>
public static class UserLimits
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 100;
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 256;
    public const int SearchMaxLength = 100;
}
