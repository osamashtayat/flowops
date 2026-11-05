using FlowOps.Domain.Errors;
using FlowOps.Domain.Organization;
using static FlowOps.Domain.Tests.TestOrg;

namespace FlowOps.Domain.Tests.Organization;

public sealed class DepartmentTests
{
    [Fact]
    public void Create_NormalizesCodeAndStartsActive()
    {
        var department = Department.Create(" Sales ", " sales-01 ", T0);

        department.Name.ShouldBe("Sales");
        department.Code.ShouldBe("SALES-01");
        department.IsActive.ShouldBeTrue();
        department.CreatedAt.ShouldBe(T0);
    }

    [Fact]
    public void Update_NormalizesAndSetsActiveFlag()
    {
        var department = Department.Create("Sales", "SALES", T0);

        department.Update(" Field Sales ", " fs-1 ", isActive: false);

        department.Name.ShouldBe("Field Sales");
        department.Code.ShouldBe("FS-1");
        department.IsActive.ShouldBeFalse();
        department.CreatedAt.ShouldBe(T0);
    }

    [Theory]
    [InlineData("", "SALES")]
    [InlineData("Sales", "SA LES")]
    public void Update_Invalid_ThrowsValidationAndKeepsState(string name, string code)
    {
        var department = Department.Create("Sales", "SALES", T0);

        ShouldFailWith(() => department.Update(name, code, isActive: false), ErrorCodes.Validation);

        department.Name.ShouldBe("Sales");
        department.Code.ShouldBe("SALES");
        department.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Sales", "SA LES")]
    [InlineData("Sales", "SALES_1")]
    [InlineData("Sales", "")]
    [InlineData("", "SALES")]
    [InlineData("Sales", "ABCDEFGHIJKLMNOPQ")]
    public void Create_Invalid_ThrowsValidation(string name, string code) =>
        ShouldFailWith(() => Department.Create(name, code, T0), ErrorCodes.Validation);
}
