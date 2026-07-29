using BuildPc.Core.Models;
using BuildPc.Core.Services;

namespace BuildPc.Core.Tests;

public sealed class CompatibilityServiceTests
{
    private readonly IReadOnlyList<PcComponent> _catalog = ComponentCatalog.CreateDefault();
    private readonly CompatibilityService _service = new();

    [Fact]
    public void CompatibleBuild_HasNoErrors()
    {
        var build = BuildWith(
            "cpu-7600", "mb-b650m", "ram-32", "gpu-4060",
            "ssd-1tb", "psu-550", "case-air", "cooler-ag400");

        var issues = _service.Evaluate(build);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Contains(issues, issue => issue.Message.Contains("compatível", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DifferentCpuAndMotherboardSockets_ReturnsError()
    {
        var build = BuildWith("cpu-14600k", "mb-b650");

        var issues = _service.Evaluate(build);

        Assert.Contains(
            issues,
            issue => issue.Severity == IssueSeverity.Error &&
                     issue.Message.Contains("Soquetes incompatíveis", StringComparison.Ordinal));
    }

    [Fact]
    public void UndersizedPowerSupply_ReturnsWarning()
    {
        var build = BuildWith("cpu-7800x3d", "gpu-7700xt", "psu-550");

        var issues = _service.Evaluate(build);

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Warning);
    }

    [Fact]
    public void TotalCost_IsTheSumOfSelectedComponents()
    {
        var build = BuildWith("cpu-7600", "ram-16", "ssd-1tb");

        var expected = Find("cpu-7600").Price + Find("ram-16").Price + Find("ssd-1tb").Price;

        Assert.Equal(expected, build.TotalCost);
    }

    [Fact]
    public void QuantitiesAndTwoStorageSlots_AreIncludedInTotalCost()
    {
        var build = new PcBuild();
        var primaryStorage = Find("ssd-1tb");
        var secondaryStorage = Find("ssd-2tb");

        build.Select(primaryStorage, ComponentCategory.Storage, "storage-primary", 2);
        build.Select(secondaryStorage, ComponentCategory.Storage, "storage-secondary", 1);

        var expected = primaryStorage.Price * 2 + secondaryStorage.Price;

        Assert.Equal(expected, build.TotalCost);
        Assert.Equal(2, build.CompletedSlots);
    }

    private PcBuild BuildWith(params string[] ids)
    {
        var build = new PcBuild();
        foreach (var id in ids)
        {
            var component = Find(id);
            build.Select(component, component.Category);
        }

        return build;
    }

    private PcComponent Find(string id) =>
        _catalog.Single(component => component.Id == id);
}
