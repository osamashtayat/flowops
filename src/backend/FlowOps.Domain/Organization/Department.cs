using FlowOps.Domain.Common;
using FlowOps.Domain.Errors;

namespace FlowOps.Domain.Organization;

/// <summary>
/// An organisational unit (DOMAIN_MODEL.md §2.1). Name/code uniqueness needs the database,
/// so it is checked in the Application layer and by unique indexes, not here.
/// </summary>
public sealed class Department
{
    public const int NameMaxLength = 100;
    public const int CodeMaxLength = 16;

    private Department(string name, string code, DateTimeOffset now)
    {
        Id = Guid.CreateVersion7(now);
        Name = name;
        Code = code;
        IsActive = true;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Upper-case <c>A-Z</c>, <c>0-9</c> and <c>-</c>.</summary>
    public string Code { get; private set; }

    /// <summary>Inactive departments cannot receive new members.</summary>
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Department Create(string name, string code, DateTimeOffset now) =>
        new(Text.Required(name, NameMaxLength, "name"), NormalizeCode(code), now);

    private static string NormalizeCode(string code)
    {
        var normalized = Text.Required(code, CodeMaxLength, "code").ToUpperInvariant();
        return normalized.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-')
            ? normalized
            : throw DomainErrors.Validation("code may contain only letters A-Z, digits and '-'.");
    }
}
