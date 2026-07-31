using BuildPc.Core.Services;
using Microsoft.Extensions.Configuration;

namespace BuildPc.Web.Services;

/// <summary>
/// Trava adicional, separada da senha da equipe, para revelar custo,
/// desconto e lucro. Sem <c>BuildPc:RevealPassword</c> configurada, o
/// recurso fica desligado (comportamento anterior: toque revela direto).
/// </summary>
/// <remarks>
/// Instância "Scoped": uma por circuito SignalR, então desbloquear uma vez
/// libera todos os <c>RevealCost</c> da sessão até a página recarregar ou o
/// circuito cair — pedir a senha de novo a cada valor seria inviável.
/// </remarks>
public sealed class RevealAccessState
{
    private readonly StaffPasswordValidator? _validator;

    public RevealAccessState(IConfiguration configuration)
    {
        var password = configuration["BuildPc:RevealPassword"];
        _validator = string.IsNullOrWhiteSpace(password)
            ? null
            : new StaffPasswordValidator(password);
        IsUnlocked = _validator is null;
    }

    public bool IsUnlocked { get; private set; }

    public bool TryUnlock(string? password)
    {
        if (IsUnlocked)
        {
            return true;
        }

        if (_validator is null || !_validator.IsValid(password))
        {
            return false;
        }

        IsUnlocked = true;
        return true;
    }
}
