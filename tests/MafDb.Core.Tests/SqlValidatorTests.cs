using MafDb.Core.Memory.Workflow;

namespace MafDb.Core.Tests;

public sealed class SqlValidatorTests
{
    private readonly SqlValidator _validator = new();

    [Fact]
    public void Validate_AllowsSelect()
    {
        var result = _validator.Validate("SELECT TOP 10 * FROM Sales.SalesOrderHeader");

        Assert.True(result.IsValid);
        Assert.Equal("SELECT TOP 10 * FROM Sales.SalesOrderHeader", result.NormalizedSql);
    }

    [Theory]
    [InlineData("DELETE FROM Sales.SalesOrderHeader")]
    [InlineData("UPDATE Person.Person SET FirstName='x'")]
    [InlineData("DROP TABLE Person.Person")]
    public void Validate_BlocksMutatingSql(string sql)
    {
        var result = _validator.Validate(sql);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Validate_BlocksMultipleStatements()
    {
        var result = _validator.Validate("SELECT 1; SELECT 2;");

        Assert.False(result.IsValid);
        Assert.Contains("Multiple", result.Error);
    }
}
