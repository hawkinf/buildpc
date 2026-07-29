using System.Security.Cryptography;
using System.Text;

namespace BuildPc.Core.Services;

internal static class BuildPcApiKeyProtector
{
    private const string Prefix = "dpapi-current-user:v1:";
    private static readonly byte[] OptionalEntropy =
        SHA256.HashData(Encoding.UTF8.GetBytes("BuildPC.ApiKey.v1"));

    public static string Protect(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "A proteção da chave de API usa o cofre criptográfico do Windows.");
        }

        var clearBytes = Encoding.UTF8.GetBytes(apiKey);
        try
        {
            var protectedBytes = ProtectedData.Protect(
                clearBytes,
                OptionalEntropy,
                DataProtectionScope.CurrentUser);
            return Prefix + Convert.ToBase64String(protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    public static string Unprotect(string protectedApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedApiKey);
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "A proteção da chave de API usa o cofre criptográfico do Windows.");
        }

        if (!protectedApiKey.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptographicException(
                "O formato da chave de API criptografada não é reconhecido.");
        }

        var protectedBytes = Convert.FromBase64String(
            protectedApiKey[Prefix.Length..]);
        var clearBytes = ProtectedData.Unprotect(
            protectedBytes,
            OptionalEntropy,
            DataProtectionScope.CurrentUser);
        try
        {
            return Encoding.UTF8.GetString(clearBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

}
