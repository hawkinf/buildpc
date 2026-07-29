using BuildPc.Core.Models;

namespace BuildPc.Core.Services;

public sealed class CompatibilityService
{
    public IReadOnlyList<CompatibilityIssue> Evaluate(PcBuild build)
    {
        var issues = new List<CompatibilityIssue>();
        var processor = build.Get(ComponentCategory.Processor);
        var motherboard = build.Get(ComponentCategory.Motherboard);
        var memory = build.Get(ComponentCategory.Memory);
        var powerSupply = build.Get(ComponentCategory.PowerSupply);
        var pcCase = build.Get(ComponentCategory.Case);
        var cooler = build.Get(ComponentCategory.Cooler);

        if (processor is not null && motherboard is not null &&
            !string.IsNullOrWhiteSpace(processor.Socket) &&
            !string.IsNullOrWhiteSpace(motherboard.Socket) &&
            !string.Equals(processor.Socket, motherboard.Socket, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(
                IssueSeverity.Error,
                $"Soquetes incompatíveis: {processor.Socket} no processador e {motherboard.Socket} na placa-mãe."));
        }

        if (memory is not null && motherboard is not null &&
            !string.IsNullOrWhiteSpace(memory.MemoryType) &&
            !string.IsNullOrWhiteSpace(motherboard.MemoryType) &&
            !string.Equals(memory.MemoryType, motherboard.MemoryType, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new(
                IssueSeverity.Error,
                $"Memória {memory.MemoryType} não é compatível com a placa-mãe {motherboard.MemoryType}."));
        }

        if (cooler is not null && processor is not null &&
            cooler.SupportedSockets.Count > 0 &&
            !cooler.SupportedSockets.Contains(processor.Socket ?? string.Empty))
        {
            issues.Add(new(
                IssueSeverity.Error,
                $"O cooler selecionado não oferece suporte ao soquete {processor.Socket}."));
        }

        if (pcCase is not null && motherboard is not null &&
            pcCase.SupportedFormFactors.Count > 0 &&
            !pcCase.SupportedFormFactors.Contains(motherboard.FormFactor ?? string.Empty))
        {
            issues.Add(new(
                IssueSeverity.Error,
                $"O gabinete não suporta placas-mãe no formato {motherboard.FormFactor}."));
        }

        if (powerSupply is not null)
        {
            var recommendedPower = build.EstimatedPowerWatts + 120;
            if (powerSupply.PowerWatts < recommendedPower)
            {
                issues.Add(new(
                    IssueSeverity.Warning,
                    $"Fonte de {powerSupply.PowerWatts} W abaixo dos {recommendedPower} W recomendados."));
            }
        }

        var missingRequired = new[]
        {
            ComponentCategory.Processor,
            ComponentCategory.Motherboard,
            ComponentCategory.Memory,
            ComponentCategory.Storage,
            ComponentCategory.PowerSupply,
            ComponentCategory.Case
        }.Count(category => build.Get(category) is null);

        if (issues.Count == 0)
        {
            issues.Add(missingRequired == 0
                ? new(IssueSeverity.Information, "Tudo certo: a configuração está compatível.")
                : new(IssueSeverity.Information, $"Continue montando: faltam {missingRequired} componentes essenciais."));
        }

        return issues;
    }
}
