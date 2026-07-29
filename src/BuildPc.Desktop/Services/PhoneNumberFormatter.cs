namespace BuildPc.Desktop.Services;

public static class PhoneNumberFormatter
{
    public static string FormatBrazilian(string? value)
    {
        var digits = new string((value ?? string.Empty)
            .Where(char.IsDigit)
            .ToArray());
        if (digits.Length > 11 && digits.StartsWith("55", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        digits = digits[..Math.Min(digits.Length, 11)];
        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (digits.Length == 1)
        {
            return $"({digits}";
        }

        if (digits.Length == 2)
        {
            return $"({digits})";
        }

        var areaCode = digits[..2];
        var subscriber = digits[2..];
        var firstPartLength = digits.Length == 11 ? 5 : Math.Min(4, subscriber.Length);
        var firstPart = subscriber[..firstPartLength];
        var secondPart = subscriber[firstPartLength..];
        return secondPart.Length == 0
            ? $"({areaCode}) {firstPart}"
            : $"({areaCode}) {firstPart}-{secondPart}";
    }
}
