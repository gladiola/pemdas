using pemdas.Services;

namespace pemdas.Tests;

public sealed class PemdasSolverTests
{
    private readonly PemdasSolver _solver = new();

    [Fact]
    public void Solve_ExplainsPemdasOrderAcrossGroupingAndMultiplication()
    {
        var result = _solver.Solve("(3 + 5) * 2");

        Assert.Null(result.ErrorMessage);
        Assert.Equal("16", result.FinalAnswer);
        Assert.Collection(
            result.Steps,
            first =>
            {
                Assert.Equal("(3 + 5) × 2", first.Before);
                Assert.Equal("(8) × 2", first.After);
            },
            second =>
            {
                Assert.Equal("(8) × 2", second.Before);
                Assert.Equal("16", second.After);
            });
    }

    [Fact]
    public void Solve_HandlesExponentBeforeAddition()
    {
        var result = _solver.Solve("2 + 3 ^ 2");

        Assert.Null(result.ErrorMessage);
        Assert.Equal("11", result.FinalAnswer);
        Assert.Collection(
            result.Steps,
            first => Assert.Contains("Evaluate exponents before", first.Explanation),
            second => Assert.Contains("Addition and subtraction", second.Explanation));
    }

    [Fact]
    public void Solve_RejectsMoreThanSixNumbers()
    {
        var result = _solver.Solve("1 + 2 + 3 + 4 + 5 + 6 + 7");

        Assert.Equal("Use no more than six numbers in one expression.", result.ErrorMessage);
        Assert.Null(result.FinalAnswer);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void Solve_RejectsDivisionByZero()
    {
        var result = _solver.Solve("10 / (5 - 5)");

        Assert.Equal("Division by zero is undefined.", result.ErrorMessage);
        Assert.Null(result.FinalAnswer);
    }
}
