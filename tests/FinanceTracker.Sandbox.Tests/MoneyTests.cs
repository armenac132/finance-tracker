using FinanceTracker.Sandbox;

namespace FinanceTracker.Sandbox.Tests;

public class MoneyTests
{
    [Fact]
    public void Add_SameCurrency_Sums()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "USD");
        Assert.Equal(15m, a.Add(b).Amount);
    }

    [Fact]
    public void Add_DifferentCurrency_Throws()
    {
        var a = new Money(10m, "USD");
        var b = new Money(5m, "EUR");
        Assert.Throws<InvalidOperationException>(() => a.Add(b));
    }
}