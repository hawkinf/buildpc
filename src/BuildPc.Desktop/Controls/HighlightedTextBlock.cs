using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using BuildPc.Core.Services;

namespace BuildPc.Desktop.Controls;

public sealed class HighlightedTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, string?>(nameof(SourceText));

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, string?>(nameof(Query));

    public static readonly StyledProperty<IBrush?> HighlightForegroundProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, IBrush?>(nameof(HighlightForeground));

    public static readonly StyledProperty<IBrush?> HighlightBackgroundProperty =
        AvaloniaProperty.Register<HighlightedTextBlock, IBrush?>(nameof(HighlightBackground));

    static HighlightedTextBlock()
    {
        SourceTextProperty.Changed.AddClassHandler<HighlightedTextBlock>(
            static (control, _) => control.RebuildInlines());
        QueryProperty.Changed.AddClassHandler<HighlightedTextBlock>(
            static (control, _) => control.RebuildInlines());
        HighlightForegroundProperty.Changed.AddClassHandler<HighlightedTextBlock>(
            static (control, _) => control.RebuildInlines());
        HighlightBackgroundProperty.Changed.AddClassHandler<HighlightedTextBlock>(
            static (control, _) => control.RebuildInlines());
    }

    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    public IBrush? HighlightForeground
    {
        get => GetValue(HighlightForegroundProperty);
        set => SetValue(HighlightForegroundProperty, value);
    }

    public IBrush? HighlightBackground
    {
        get => GetValue(HighlightBackgroundProperty);
        set => SetValue(HighlightBackgroundProperty, value);
    }

    private void RebuildInlines()
    {
        var text = SourceText ?? string.Empty;
        var ranges = ProductFilter.GetHighlightRanges(text, Query);
        var inlines = new InlineCollection();

        if (ranges.Count == 0)
        {
            inlines.Add(new Run(text));
            Inlines = inlines;
            return;
        }

        var position = 0;
        foreach (var (start, length) in ranges)
        {
            if (start > position)
            {
                inlines.Add(new Run(text[position..start]));
            }

            inlines.Add(new Run(text.Substring(start, length))
            {
                Foreground = HighlightForeground,
                Background = HighlightBackground,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });
            position = start + length;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }

        Inlines = inlines;
    }
}
