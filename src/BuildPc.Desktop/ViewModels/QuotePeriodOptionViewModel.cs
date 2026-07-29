using BuildPc.Core.Services;

namespace BuildPc.Desktop.ViewModels;

public sealed class QuotePeriodOptionViewModel(QuotePeriod period)
{
    public QuotePeriod Period { get; } = period;
    public string Name { get; } = QuoteFilter.DisplayName(period);
}
