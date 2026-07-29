using BuildPc.Desktop.Services;

namespace BuildPc.Core.Tests;

public sealed class PhoneNumberFormatterTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("1", "(1")]
    [InlineData("11", "(11)")]
    [InlineData("1133334444", "(11) 3333-4444")]
    [InlineData("11987654321", "(11) 98765-4321")]
    [InlineData("(11) 98765-4321", "(11) 98765-4321")]
    [InlineData("+55 11 98765-4321", "(11) 98765-4321")]
    public void FormatBrazilian_AppliesPhoneMask(string input, string expected)
    {
        Assert.Equal(expected, PhoneNumberFormatter.FormatBrazilian(input));
    }

    [Fact]
    public void SystemFileLauncher_DoesNotOpenMissingFile()
    {
        Assert.False(SystemFileLauncher.Open(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf")));
    }
}
