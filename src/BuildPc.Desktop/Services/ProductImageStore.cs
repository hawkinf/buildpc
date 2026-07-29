using BuildPc.Core.Models;

namespace BuildPc.Desktop.Services;

/// <summary>
/// Guarda as fotos escolhidas manualmente em
/// <c>%LocalAppData%\BuildPC\imagens-produtos</c> e apaga as que deixaram de
/// ser usadas.
/// </summary>
/// <remarks>
/// Sem a limpeza, trocar ou excluir produtos ia acumulando arquivos que nenhum
/// produto referencia. Só arquivos dentro dessa pasta são removidos: uma foto
/// apontada de outro lugar do disco pertence ao usuário.
/// </remarks>
public sealed class ProductImageStore(string imagesDirectory)
{
    private const long MaximumImageBytes = 5 * 1024 * 1024;

    public string Directory => imagesDirectory;

    /// <summary>
    /// Copia a foto escolhida para a pasta do BuildPC e devolve o caminho final.
    /// URLs http/https são mantidas como estão. Devolve <c>null</c> quando não
    /// há foto.
    /// </summary>
    public string? Persist(string componentId, string selectedPath)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return null;
        }

        if (Uri.TryCreate(selectedPath, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            return uri.AbsoluteUri;
        }

        var sourcePath = uri?.IsFile == true ? uri.LocalPath : selectedPath;
        if (!File.Exists(sourcePath))
        {
            throw new IOException("A foto selecionada não existe.");
        }

        if (new FileInfo(sourcePath).Length > MaximumImageBytes)
        {
            throw new IOException("A foto selecionada deve ter no máximo 5 MB.");
        }

        System.IO.Directory.CreateDirectory(imagesDirectory);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        if (IsStoredHere(fullSourcePath))
        {
            return fullSourcePath;
        }

        var safeId = string.Concat(componentId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var destinationPath = Path.Combine(
            imagesDirectory,
            $"{safeId}-{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}");
        File.Copy(sourcePath, destinationPath, overwrite: false);
        return destinationPath;
    }

    /// <summary>
    /// Apaga uma foto guardada que deixou de ser referenciada. Ignora URLs,
    /// caminhos fora da pasta do BuildPC e o arquivo que continua em uso.
    /// </summary>
    public void DeleteIfUnused(string? previousPath, string? currentPath = null)
    {
        if (string.IsNullOrWhiteSpace(previousPath))
        {
            return;
        }

        if (string.Equals(
                previousPath,
                currentPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(previousPath);
            if (IsStoredHere(fullPath) && File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Uma foto aberta em outro programa fica para trás; não é motivo
            // para interromper a operação do usuário.
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ArgumentException)
        {
            // Caminho inválido gravado por uma versão antiga.
        }
    }

    public void DeleteIfUnused(IEnumerable<PcComponent> removedComponents)
    {
        ArgumentNullException.ThrowIfNull(removedComponents);
        foreach (var component in removedComponents)
        {
            DeleteIfUnused(component.ImageUrl);
        }
    }

    private bool IsStoredHere(string fullPath)
    {
        var root = Path.GetFullPath(imagesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
