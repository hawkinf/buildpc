using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace BuildPc.Core.Services;

/// <summary>
/// Valida a senha única compartilhada pela equipe (sem cadastro de usuários).
/// </summary>
/// <remarks>
/// Usa <see cref="PasswordHasher{TUser}"/> em vez de comparar strings: o hash
/// é calculado uma única vez, na inicialização, e cada tentativa de login é
/// verificada contra ele com <c>VerifyHashedPassword</c> (PBKDF2, resistente
/// a ataques de tempo).
/// </remarks>
public sealed class StaffPasswordValidator
{
    // Identidade fictícia exigida pela assinatura genérica do PasswordHasher;
    // não há usuário real por trás da senha compartilhada.
    private static readonly object HasherIdentity = new();

    private readonly PasswordHasher<object> _hasher = new();
    private readonly string _hashedPassword;

    public StaffPasswordValidator(string configuredPassword)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPassword);
        _hashedPassword = _hasher.HashPassword(HasherIdentity, configuredPassword);
    }

    public bool IsValid(string? suppliedPassword)
    {
        if (string.IsNullOrEmpty(suppliedPassword))
        {
            return false;
        }

        var result = _hasher.VerifyHashedPassword(
            HasherIdentity,
            _hashedPassword,
            suppliedPassword);
        return result is PasswordVerificationResult.Success or
            PasswordVerificationResult.SuccessRehashNeeded;
    }

    public static StaffPasswordValidator FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var password = configuration["BuildPc:WebPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "A senha compartilhada 'BuildPc:WebPassword' não foi configurada.");
        }

        return new StaffPasswordValidator(password);
    }
}
