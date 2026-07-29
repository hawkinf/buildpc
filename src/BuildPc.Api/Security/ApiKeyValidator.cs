using System.Security.Cryptography;
using System.Text;

namespace BuildPc.Api.Security;

/// <summary>
/// Valida a chave de acesso aceitando mais de uma chave válida ao mesmo tempo.
/// </summary>
/// <remarks>
/// Com uma única chave, trocá-la exigia atualizar o servidor e todas as
/// instalações no mesmo instante — em prática, ninguém troca. Aceitar a chave
/// nova e a anterior permite a rotação em duas etapas: publica-se a nova,
/// atualizam-se os clientes com calma, depois remove-se a antiga.
/// </remarks>
public sealed class ApiKeyValidator
{
    private readonly byte[][] _acceptedHashes;

    public ApiKeyValidator(IEnumerable<string> acceptedKeys)
    {
        ArgumentNullException.ThrowIfNull(acceptedKeys);
        _acceptedHashes = acceptedKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .Select(key => SHA256.HashData(Encoding.UTF8.GetBytes(key)))
            .ToArray();

        if (_acceptedHashes.Length == 0)
        {
            throw new InvalidOperationException(
                "Nenhuma chave de acesso válida foi configurada.");
        }
    }

    public int AcceptedKeyCount => _acceptedHashes.Length;

    /// <summary>
    /// Compara a chave recebida com todas as aceitas em tempo constante.
    /// </summary>
    public bool IsValid(string? suppliedKey)
    {
        if (string.IsNullOrEmpty(suppliedKey))
        {
            return false;
        }

        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey));

        // Percorre todas as chaves sempre, sem interromper no primeiro acerto,
        // para o tempo de resposta não indicar qual delas casou.
        var matched = false;
        foreach (var accepted in _acceptedHashes)
        {
            matched |= CryptographicOperations.FixedTimeEquals(suppliedHash, accepted);
        }

        return matched;
    }

    /// <summary>
    /// Lê a chave em uso e, opcionalmente, a anterior ainda aceita durante a
    /// rotação (<c>BuildPc:PreviousApiKey</c>).
    /// </summary>
    public static ApiKeyValidator FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var current = configuration["BuildPc:ApiKey"];
        if (string.IsNullOrWhiteSpace(current))
        {
            throw new InvalidOperationException(
                "A chave de acesso 'BuildPc:ApiKey' não foi configurada.");
        }

        return new ApiKeyValidator([current, configuration["BuildPc:PreviousApiKey"] ?? string.Empty]);
    }
}
