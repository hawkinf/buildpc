using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class QuoteFilterTests
{
    [Theory]
    [InlineData("000042")]
    [InlineData("42")]
    [InlineData("maria")]
    [InlineData("MARIA")]
    [InlineData("98765")]
    [InlineData("(11) 98765-4321")]
    [InlineData("11987654321")]
    [InlineData("ryzen")]
    [InlineData("processador")]
    [InlineData("maria ryzen")]
    public void MatchesFindsQuoteByNumberClientPhoneAndProduct(string search)
    {
        Assert.True(QuoteFilter.Matches(Quote(), search));
    }

    [Theory]
    [InlineData("joao")]
    [InlineData("999999")]
    [InlineData("maria geforce")]
    public void MatchesRejectsWhenAnyTermIsMissing(string search)
    {
        Assert.False(QuoteFilter.Matches(Quote(), search));
    }

    [Fact]
    public void EmptySearchMatchesEverything()
    {
        Assert.True(QuoteFilter.Matches(Quote(), null));
        Assert.True(QuoteFilter.Matches(Quote(), string.Empty));
        Assert.True(QuoteFilter.Matches(Quote(), "   "));
    }

    [Theory]
    [InlineData(QuotePeriod.All, 400, true)]
    [InlineData(QuotePeriod.Last7Days, 3, true)]
    [InlineData(QuotePeriod.Last7Days, 10, false)]
    [InlineData(QuotePeriod.Last30Days, 20, true)]
    [InlineData(QuotePeriod.Last30Days, 45, false)]
    [InlineData(QuotePeriod.Last90Days, 80, true)]
    [InlineData(QuotePeriod.Last90Days, 120, false)]
    public void IsInPeriodRespectsTheChosenWindow(
        QuotePeriod period,
        int daysAgo,
        bool expected)
    {
        var now = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.FromHours(-3));
        var quote = Quote() with { CreatedAt = now.AddDays(-daysAgo) };

        Assert.Equal(expected, QuoteFilter.IsInPeriod(quote, period, now));
    }

    [Fact]
    public void ThisYearComparesTheCalendarYearNotTheLast365Days()
    {
        var now = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.FromHours(-3));
        var lateLastYear = Quote() with
        {
            CreatedAt = new DateTimeOffset(2025, 12, 28, 12, 0, 0, TimeSpan.FromHours(-3))
        };
        var earlyThisYear = Quote() with
        {
            CreatedAt = new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.FromHours(-3))
        };

        // Poucos dias antes, mas de outro ano: fica fora.
        Assert.False(QuoteFilter.IsInPeriod(lateLastYear, QuotePeriod.ThisYear, now));
        Assert.True(QuoteFilter.IsInPeriod(earlyThisYear, QuotePeriod.ThisYear, now));
    }

    [Fact]
    public void DisplayNameCoversEveryPeriod()
    {
        foreach (var period in Enum.GetValues<QuotePeriod>())
        {
            Assert.False(string.IsNullOrWhiteSpace(QuoteFilter.DisplayName(period)));
        }
    }

    private static SavedQuote Quote() =>
        new()
        {
            Id = Guid.NewGuid(),
            Number = 42,
            CreatedAt = DateTimeOffset.Now,
            ClientName = "Maria Oliveira",
            ClientPhone = "(11) 98765-4321",
            Notes = "Entrega combinada",
            Items =
            [
                new SavedQuoteItem
                {
                    ComponentId = "cpu",
                    Category = ComponentCategory.Processor,
                    CategoryName = "Processador",
                    Name = "AMD Ryzen 7 7800X3D",
                    Quantity = 1,
                    UnitCost = 2000m,
                    UnitPrice = 2500m
                }
            ]
        };
}
