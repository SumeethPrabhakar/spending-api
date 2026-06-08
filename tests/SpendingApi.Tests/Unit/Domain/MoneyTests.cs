using SpendingApi.Domain.ValueObjects;

namespace SpendingApi.Tests.Unit.Domain;

public sealed class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_Succeeds()
    {
        var result = Money.Create(100.50m);

        Assert.True(result.IsSuccess);
        Assert.Equal(100.50m, result.Value.Amount);
        Assert.Equal("AUD", result.Value.Currency);
    }

    [Fact]
    public void Create_WithNegativeAmount_Fails()
    {
        var result = Money.Create(-1m);

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public void Create_WithZeroAmount_Succeeds()
    {
        var result = Money.Create(0m);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Amount);
    }

    [Fact]
    public void Create_NormalisesCurrencyToUppercase()
    {
        var result = Money.Create(10m, "aud");

        Assert.True(result.IsSuccess);
        Assert.Equal("AUD", result.Value.Currency);
    }

    [Fact]
    public void Create_WithEmptyCurrency_Fails()
    {
        var result = Money.Create(10m, "");

        Assert.True(result.IsFailure);
        Assert.Equal("VALIDATION", result.Error.Code);
    }

    [Fact]
    public void Add_SameCurrency_ReturnsSummedMoney()
    {
        var a = Money.Create(100m).Value;
        var b = Money.Create(50m).Value;

        var sum = a.Add(b);

        Assert.Equal(150m, sum.Amount);
        Assert.Equal("AUD", sum.Currency);
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        var aud = Money.Create(100m, "AUD").Value;
        var usd = Money.Create(100m, "USD").Value;

        Assert.Throws<InvalidOperationException>(() => aud.Add(usd));
    }

    [Fact]
    public void ToString_FormatsCorrectly()
    {
        var money = Money.Create(1234.5m).Value;

        Assert.Equal("AUD 1234.50", money.ToString());
    }
}
